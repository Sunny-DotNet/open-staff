using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Application.Orchestration;
using OpenStaff.Core.Models;
using OpenStaff.Core.Notifications;
using OpenStaff.Core.Orchestration;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Application.Sessions;

/// <summary>
/// 会话执行引擎 — 管理栈模式 Frame 的推入/弹出和 Agent 调用
/// 支持群聊模式：一个项目一个长期 Session，用户可随时追加消息
/// </summary>
public class SessionRunner
{
    private readonly SessionStreamManager _streamManager;
    private readonly INotificationService _notification;
    private readonly OrchestrationService _orchestration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionRunner> _logger;

    // 活跃 Session 的 CancellationTokenSource
    private readonly ConcurrentDictionary<Guid, (CancellationTokenSource cts, DateTime createdAt)> _sessionCts = new();
    // 活跃 Frame 的 CancellationTokenSource
    private readonly ConcurrentDictionary<Guid, (CancellationTokenSource cts, DateTime createdAt)> _frameCts = new();
    // 活跃 Session 的当前 Frame ID
    private readonly ConcurrentDictionary<Guid, Guid> _currentFrame = new();
    // 暂停等待用户输入的信号量（sessionId → 信号量）
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<string>> _awaitingInput = new();

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

        _streamManager.Create(session.Id);

        var sessionCts = new CancellationTokenSource();
        _sessionCts[session.Id] = (sessionCts, DateTime.UtcNow);

        await PushEventAsync(session.Id, SessionEventTypes.SessionCreated, payload: new
        {
            sessionId = session.Id,
            projectId,
            input
        });

        await _notification.NotifyAsync(Channels.Project(projectId), "session_started", new
        {
            sessionId = session.Id,
            input = Truncate(input, 100)
        });

        // 后台执行第一轮
        _ = Task.Run(() => ExecuteUserInputAsync(session, input, sessionCts.Token));

        return session;
    }

    /// <summary>
    /// 群聊追加消息 — 向已有 Session 发送新消息
    /// 如果 Session 正在等待用户输入，恢复链式流转；
    /// 否则创建新的根 Frame 执行。
    /// </summary>
    public async Task SendMessageAsync(Guid sessionId, string input)
    {
        // 检查是否正在等待用户输入
        if (_awaitingInput.TryRemove(sessionId, out var tcs))
        {
            _logger.LogInformation("Session {SessionId} resumed with user input", sessionId);

            // 更新 Session 状态回 Active
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.ChatSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.Status = SessionStatus.Active;
                await db.SaveChangesAsync();
            }

            await PushEventAsync(sessionId, SessionEventTypes.ResumedByUser, payload: new
            {
                input
            });

            // 唤醒等待的链路
            tcs.TrySetResult(input);
            return;
        }

        // 非暂停状态 — 创建新的根 Frame 执行
        ChatSession? existing;
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            existing = await db.ChatSessions.FindAsync(sessionId);
        }

        if (existing == null)
        {
            _logger.LogWarning("Session {SessionId} not found", sessionId);
            return;
        }

        // 确保流存在
        if (!_streamManager.IsActive(sessionId))
        {
            _streamManager.Create(sessionId);
        }

        // 确保 CTS 存在
        if (!_sessionCts.ContainsKey(sessionId))
        {
            var cts = new CancellationTokenSource();
            _sessionCts[sessionId] = (cts, DateTime.UtcNow);
        }

        var sessionCts = _sessionCts[sessionId].cts;

        // 发布用户输入事件到群聊
        await PushEventAsync(sessionId, SessionEventTypes.UserInput, payload: new
        {
            input,
            role = "user"
        });

        // 后台执行
        _ = Task.Run(() => ExecuteUserInputAsync(existing, input, sessionCts.Token));
    }

    /// <summary>
    /// 执行用户输入 — 创建根 Frame 并执行链式流转
    /// </summary>
    private async Task ExecuteUserInputAsync(ChatSession session, string input, CancellationToken sessionCt)
    {
        try
        {
            await _orchestration.InitializeProjectAgentsAsync(session.ProjectId, sessionCt);

            // 先经过 orchestrator 决定路由（对话者优先）
            var rootFrame = await CreateFrameAsync(session.Id, null, 0, "user", "communicator", input, sessionCt);

            await PushEventAsync(session.Id, SessionEventTypes.FramePushed, rootFrame.Id, new
            {
                frameId = rootFrame.Id,
                depth = 0,
                initiator = "user",
                target = "communicator",
                purpose = input
            });

            await ExecuteFrameAsync(session, rootFrame, sessionCt);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Session {SessionId} execution cancelled", session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} execution failed", session.Id);
            await PushEventAsync(session.Id, SessionEventTypes.Error, payload: new { error = ex.Message });
        }
    }

    /// <summary>
    /// 取消整个会话
    /// </summary>
    public async Task CancelSessionAsync(Guid sessionId)
    {
        // 清理暂停信号
        if (_awaitingInput.TryRemove(sessionId, out var tcs))
        {
            tcs.TrySetCanceled();
        }

        if (_sessionCts.TryRemove(sessionId, out var sessionData))
        {
            await sessionData.cts.CancelAsync();
            sessionData.cts.Dispose();
            _logger.LogInformation("Session {SessionId} cancelled", sessionId);
        }
        await _streamManager.CancelAsync(sessionId, "Cancelled by user");

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await db.ChatSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.Status = SessionStatus.Cancelled;
            session.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Pop 当前 Frame（只取消当前帧，不影响整个会话）
    /// </summary>
    public void PopCurrentFrame(Guid sessionId)
    {
        if (_currentFrame.TryGetValue(sessionId, out var frameId))
        {
            if (_frameCts.TryRemove(frameId, out var frameData))
            {
                frameData.cts.Cancel();
                frameData.cts.Dispose();
                _logger.LogDebug("Frame {FrameId} popped for session {SessionId}", frameId, sessionId);
            }
        }
    }

    /// <summary>
    /// 检查 Session 是否正在等待用户输入
    /// </summary>
    public bool IsAwaitingInput(Guid sessionId) => _awaitingInput.ContainsKey(sessionId);

    /// <summary>
    /// 执行单个 Frame — 调用目标 Agent，处理路由和子 Frame
    /// </summary>
    private async Task<string> ExecuteFrameAsync(
        ChatSession session, ChatFrame frame, CancellationToken sessionCt)
    {
        using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(sessionCt);
        _frameCts[frame.Id] = (frameCts, DateTime.UtcNow);
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
            var response = await _orchestration.RouteToAgentAsync(
                session.ProjectId, frame.TargetRole ?? "communicator", frame.Purpose, ct);

            // 发布消息事件（群聊中显示 Agent 发言）
            await PushEventAsync(session.Id, SessionEventTypes.Message, frame.Id, new
            {
                agent = frame.TargetRole,
                content = response.Content,
                success = response.Success
            });

            // 保存消息到数据库
            await SaveMessageAsync(frame, "assistant", frame.TargetRole, response.Content ?? "", ct);

            // 检查是否需要用户输入（暂停链式流转）
            if (response.RequiresUserInput)
            {
                await PauseForUserInputAsync(session, frame, ct);
                // 暂停恢复后，不再继续当前链路（用户新消息会触发新的 ExecuteUserInputAsync）
                await CompleteFrameAsync(frame.Id, response.Content ?? "", ct);
                return response.Content ?? "";
            }

            // 检查是否需要路由到下一个 Agent（链式流转）
            if (!string.IsNullOrEmpty(response.TargetRole) && response.TargetRole != frame.TargetRole)
            {
                await PushEventAsync(session.Id, SessionEventTypes.Routing, frame.Id, new
                {
                    from = frame.TargetRole,
                    to = response.TargetRole,
                    reason = "Agent routing marker"
                });

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

                var childResult = await ExecuteFrameAsync(session, childFrame, sessionCt);

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

    /// <summary>
    /// 暂停 Session 等待用户输入
    /// </summary>
    private async Task PauseForUserInputAsync(ChatSession session, ChatFrame frame, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _awaitingInput[session.Id] = tcs;

        // 更新 Session 状态
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbSession = await db.ChatSessions.FindAsync(session.Id);
            if (dbSession != null)
            {
                dbSession.Status = SessionStatus.AwaitingInput;
                await db.SaveChangesAsync();
            }
        }

        await PushEventAsync(session.Id, SessionEventTypes.AwaitingInput, frame.Id, new
        {
            agent = frame.TargetRole,
            message = "等待用户输入..."
        });

        _logger.LogInformation("Session {SessionId} paused, awaiting user input", session.Id);

        // 等待用户输入或取消
        using var reg = ct.Register(() => tcs.TrySetCanceled());
        await tcs.Task; // 阻塞直到用户回复或取消
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
