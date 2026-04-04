using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agents.Orchestrator;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Core.Notifications;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Api.Services;

/// <summary>
/// 会话执行引擎 — 管理栈模式 Frame 的推入/弹出和 Agent 调用
/// </summary>
public class SessionRunner
{
    private readonly SessionStreamManager _streamManager;
    private readonly INotificationService _notification;
    private readonly OrchestrationService _orchestration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionRunner> _logger;

    // 活跃 Session 的 CancellationTokenSource
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sessionCts = new();
    // 活跃 Frame 的 CancellationTokenSource
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _frameCts = new();
    // 活跃 Session 的当前 Frame ID
    private readonly ConcurrentDictionary<Guid, Guid> _currentFrame = new();

    public SessionRunner(
        SessionStreamManager streamManager,
        INotificationService notification,
        OrchestrationService orchestration,
        IServiceProvider serviceProvider,
        ILogger<SessionRunner> logger)
    {
        _streamManager = streamManager;
        _notification = notification;
        _orchestration = orchestration;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 启动新会话 — 创建 Session + 初始 Frame，后台异步执行
    /// </summary>
    public async Task<ChatSession> StartSessionAsync(Guid projectId, string input, string contextStrategy = ContextStrategies.Full)
    {
        // 1. 创建会话记录
        var session = new ChatSession
        {
            ProjectId = projectId,
            InitialInput = input,
            ContextStrategy = contextStrategy
        };

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ChatSessions.Add(session);
            await db.SaveChangesAsync();
        }

        // 2. 创建 ReplaySubject 流
        _streamManager.Create(session.Id);

        // 3. 创建 Session 级 CancellationTokenSource
        var sessionCts = new CancellationTokenSource();
        _sessionCts[session.Id] = sessionCts;

        // 4. 发布会话创建事件
        await PushEventAsync(session.Id, SessionEventTypes.SessionCreated, payload: new
        {
            sessionId = session.Id,
            projectId,
            input
        });

        // 5. 通知项目频道有新会话
        await _notification.NotifyAsync(Channels.Project(projectId), "session_started", new
        {
            sessionId = session.Id,
            input = Truncate(input, 100)
        });

        // 6. 后台执行
        _ = Task.Run(async () =>
        {
            try
            {
                await RunSessionAsync(session, sessionCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Session {SessionId} was cancelled", session.Id);
                await FinalizeSessionAsync(session.Id, SessionStatus.Cancelled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session {SessionId} failed", session.Id);
                await PushEventAsync(session.Id, SessionEventTypes.SessionError, payload: ex.Message);
                await FinalizeSessionAsync(session.Id, SessionStatus.Error);
            }
        });

        return session;
    }

    /// <summary>
    /// 取消整个会话
    /// </summary>
    public async Task CancelSessionAsync(Guid sessionId)
    {
        if (_sessionCts.TryRemove(sessionId, out var cts))
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
        await _streamManager.CancelAsync(sessionId, "Cancelled by user");
    }

    /// <summary>
    /// Pop 当前 Frame（只取消当前帧，不影响整个会话）
    /// </summary>
    public void PopCurrentFrame(Guid sessionId)
    {
        if (_currentFrame.TryGetValue(sessionId, out var frameId))
        {
            if (_frameCts.TryRemove(frameId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    }

    /// <summary>
    /// 会话主循环 — 从 Orchestrator 路由开始，递归执行 Frame 栈
    /// </summary>
    private async Task RunSessionAsync(ChatSession session, CancellationToken sessionCt)
    {
        // 确保项目 Agents 已初始化
        await _orchestration.InitializeProjectAgentsAsync(session.ProjectId, sessionCt);

        // 创建根 Frame（Orchestrator 路由）
        var rootFrame = await CreateFrameAsync(session.Id, null, 0, "user", "orchestrator", session.InitialInput, sessionCt);

        await PushEventAsync(session.Id, SessionEventTypes.FramePushed, rootFrame.Id, new
        {
            frameId = rootFrame.Id,
            depth = 0,
            initiator = "user",
            target = "orchestrator",
            purpose = session.InitialInput
        });

        // 执行 Frame 栈
        var result = await ExecuteFrameAsync(session, rootFrame, sessionCt);

        // 会话完成
        await PushEventAsync(session.Id, SessionEventTypes.SessionCompleted, payload: new
        {
            sessionId = session.Id,
            result
        });

        await FinalizeSessionAsync(session.Id, SessionStatus.Completed, result);
    }

    /// <summary>
    /// 执行单个 Frame — 调用目标 Agent，处理路由和子 Frame
    /// </summary>
    private async Task<string> ExecuteFrameAsync(
        ChatSession session, ChatFrame frame, CancellationToken sessionCt)
    {
        // 创建 Frame 级 CancellationToken（linked to session）
        using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(sessionCt);
        _frameCts[frame.Id] = frameCts;
        _currentFrame[session.Id] = frame.Id;

        try
        {
            var ct = frameCts.Token;

            // 发送思考事件
            await PushEventAsync(session.Id, SessionEventTypes.Thought, frame.Id, new
            {
                agent = frame.TargetRole,
                message = $"正在处理: {Truncate(frame.Purpose, 100)}"
            });

            // 调用目标 Agent
            var message = new AgentMessage
            {
                Content = frame.Purpose,
                FromRole = frame.InitiatorRole,
                Timestamp = DateTime.UtcNow
            };

            var response = await _orchestration.RouteToAgentAsync(
                session.ProjectId, frame.TargetRole ?? "communicator", message, ct);

            // 发布消息事件
            await PushEventAsync(session.Id, SessionEventTypes.Message, frame.Id, new
            {
                agent = frame.TargetRole,
                content = response.Content,
                success = response.Success
            });

            // 保存消息到数据库
            await SaveMessageAsync(frame, "assistant", frame.TargetRole, response.Content ?? "", ct);

            // 检查是否需要路由到下一个 Agent（生成子 Frame）
            if (!string.IsNullOrEmpty(response.TargetRole) && response.TargetRole != frame.TargetRole)
            {
                await PushEventAsync(session.Id, SessionEventTypes.Routing, frame.Id, new
                {
                    from = frame.TargetRole,
                    to = response.TargetRole,
                    reason = "Agent routing marker"
                });

                // 创建子 Frame
                var childFrame = await CreateFrameAsync(
                    session.Id, frame.Id, frame.Depth + 1,
                    frame.TargetRole ?? "unknown",
                    response.TargetRole,
                    response.Content ?? frame.Purpose,
                    ct);

                await PushEventAsync(session.Id, SessionEventTypes.FramePushed, childFrame.Id, new
                {
                    frameId = childFrame.Id,
                    depth = childFrame.Depth,
                    initiator = frame.TargetRole,
                    target = response.TargetRole,
                    purpose = Truncate(response.Content, 200)
                });

                // 递归执行子 Frame
                var childResult = await ExecuteFrameAsync(session, childFrame, sessionCt);

                // 子 Frame 完成后，Pop 回到当前 Frame
                await PushEventAsync(session.Id, SessionEventTypes.FrameCompleted, childFrame.Id, new
                {
                    frameId = childFrame.Id,
                    result = Truncate(childResult, 200)
                });

                return childResult;
            }

            // 当前 Frame 完成
            await CompleteFrameAsync(frame.Id, response.Content ?? "", ct);

            await PushEventAsync(session.Id, SessionEventTypes.FrameCompleted, frame.Id, new
            {
                frameId = frame.Id,
                result = Truncate(response.Content, 200)
            });

            return response.Content ?? "";
        }
        catch (OperationCanceledException) when (frameCts.IsCancellationRequested && !sessionCt.IsCancellationRequested)
        {
            // Frame 级取消（Pop）
            await CompleteFrameAsync(frame.Id, "Frame popped by user", CancellationToken.None, FrameStatus.Popped);

            await PushEventAsync(session.Id, SessionEventTypes.FramePopped, frame.Id, new
            {
                frameId = frame.Id,
                reason = "User requested pop"
            });

            return "Frame popped by user";
        }
        finally
        {
            _frameCts.TryRemove(frame.Id, out _);
            _currentFrame.TryRemove(session.Id, out _);
        }
    }

    private async Task<ChatFrame> CreateFrameAsync(
        Guid sessionId, Guid? parentFrameId, int depth,
        string initiatorRole, string targetRole, string purpose,
        CancellationToken ct)
    {
        var frame = new ChatFrame
        {
            SessionId = sessionId,
            ParentFrameId = parentFrameId,
            Depth = depth,
            InitiatorRole = initiatorRole,
            TargetRole = targetRole,
            Purpose = purpose
        };

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChatFrames.Add(frame);

        // 同时保存用户输入消息
        var userMsg = new Core.Models.ChatMessage
        {
            FrameId = frame.Id,
            SessionId = sessionId,
            Role = MessageRoles.User,
            Content = purpose,
            SequenceNo = 0
        };
        db.ChatMessages.Add(userMsg);

        await db.SaveChangesAsync(ct);
        return frame;
    }

    private async Task SaveMessageAsync(ChatFrame frame, string role, string? agentRole, string content, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var maxSeq = await db.ChatMessages
            .Where(m => m.FrameId == frame.Id)
            .MaxAsync(m => (int?)m.SequenceNo, ct) ?? 0;

        db.ChatMessages.Add(new Core.Models.ChatMessage
        {
            FrameId = frame.Id,
            SessionId = frame.SessionId,
            Role = role,
            AgentRole = agentRole,
            Content = content,
            SequenceNo = maxSeq + 1
        });

        await db.SaveChangesAsync(ct);
    }

    private async Task CompleteFrameAsync(Guid frameId, string result, CancellationToken ct, string status = FrameStatus.Completed)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var frame = await db.ChatFrames.FindAsync(new object[] { frameId }, ct);
        if (frame != null)
        {
            frame.Status = status;
            frame.Result = result;
            frame.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task FinalizeSessionAsync(Guid sessionId, string status, string? result = null)
    {
        // 更新数据库状态
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await db.ChatSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.Status = status;
            session.FinalResult = result;
            session.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        // 持久化事件并释放流
        if (status == SessionStatus.Cancelled)
        {
            await _streamManager.CancelAsync(sessionId);
        }
        else
        {
            await _streamManager.CompleteAsync(sessionId);
        }

        // 清理 CancellationTokenSource
        if (_sessionCts.TryRemove(sessionId, out var cts))
            cts.Dispose();
    }

    private static string? Truncate(string? text, int maxLength)
    {
        if (text == null) return null;
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    /// <summary>
    /// 通过 INotificationService 发布会话事件
    /// </summary>
    private Task PushEventAsync(Guid sessionId, string eventType, Guid? frameId = null, object? payload = null)
    {
        var evt = new SessionEvent
        {
            SessionId = sessionId,
            FrameId = frameId,
            EventType = eventType,
            Payload = payload != null ? JsonSerializer.Serialize(payload) : null,
            CreatedAt = DateTime.UtcNow
        };
        return _notification.PublishSessionEventAsync(sessionId, evt);
    }
}
