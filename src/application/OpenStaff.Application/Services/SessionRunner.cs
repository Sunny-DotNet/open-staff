using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent.Services;
using OpenStaff.Application.Conversations.Models;
using OpenStaff.Application.Orchestration.Services;
using OpenStaff.Application.Projects.Services;
using OpenStaff.ApiServices;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.Dtos;
using OpenStaff.Core.Notifications;
using OpenStaff.Core.Orchestration;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Sessions.Services;
/// <summary>
/// 会话执行引擎，负责栈式 Frame 推进、群聊续写与路由执行。
/// Session execution engine that advances stacked frames, resumes group chats, and drives routed execution.
/// </summary>
public class SessionRunner
{
    private static readonly TimeSpan DefaultProjectGroupLeaseTimeout = TimeSpan.FromMinutes(5);

    private readonly SessionStreamManager _streamManager;
    private readonly INotificationService _notification;
    private readonly OrchestrationService _orchestration;
    private readonly IAgentService _agentService;
    private readonly ProjectGroupExecutionService _projectGroupExecution;
    private readonly ProjectGroupCapabilityService _projectGroupCapability;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionRunner> _logger;
    private readonly TimeSpan _projectGroupLeaseTimeout;

    // zh-CN: Session 级取消令牌负责整条链路的终止，Frame 级取消令牌则只影响当前栈顶执行。
    // en: Session-level cancellation stops the whole chain, while frame-level cancellation only affects the current stack top.
    private readonly ConcurrentDictionary<Guid, (CancellationTokenSource cts, DateTime createdAt)> _sessionCts = new();
    private readonly ConcurrentDictionary<Guid, (CancellationTokenSource cts, DateTime createdAt)> _frameCts = new();

    // zh-CN: 当前帧映射和等待输入映射共同维护“暂停后继续”的会话状态机。
    // en: The current-frame map and awaiting-input map together maintain the pause-and-resume session state machine.
    private readonly ConcurrentDictionary<Guid, Guid> _currentFrame = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<string>> _awaitingInput = new();

    /// <summary>
    /// 初始化会话执行引擎。
    /// Initializes the session execution engine.
    /// </summary>
    public SessionRunner(
        SessionStreamManager streamManager,
        INotificationService notification,
        OrchestrationService orchestration,
        IAgentService agentService,
        ProjectGroupExecutionService projectGroupExecution,
        ProjectGroupCapabilityService projectGroupCapability,
        IServiceScopeFactory scopeFactory,
        ILogger<SessionRunner> logger,
        TimeSpan? projectGroupLeaseTimeout = null)
    {
        _streamManager = streamManager;
        _notification = notification;
        _orchestration = orchestration;
        _agentService = agentService;
        _projectGroupExecution = projectGroupExecution;
        _projectGroupCapability = projectGroupCapability;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _projectGroupLeaseTimeout = projectGroupLeaseTimeout ?? DefaultProjectGroupLeaseTimeout;
    }

    /// <summary>
    /// 启动新会话；若同场景已有活跃会话则直接续写。
    /// Starts a new session and resumes an existing active one for the same scene when available.
    /// </summary>
    public async Task<ChatSession> StartSessionAsync(
        Guid projectId,
        string input,
        string contextStrategy = ContextStrategies.Full,
        string scene = SessionSceneTypes.ProjectBrainstorm,
        IReadOnlyList<ConversationMention>? mentions = null,
        string? rawInput = null)
    {
        var task = await StartSessionTaskCoreAsync(projectId, input, contextStrategy, scene, mentions, rawInput);
        return task.Session;
    }

    /// <summary>
    /// 启动一轮新的会话任务，并返回统一任务引用。
    /// Starts a new session turn and returns the unified conversation-task reference.
    /// </summary>
    public async Task<ConversationTaskOutput> StartSessionTaskAsync(
        Guid projectId,
        string input,
        string contextStrategy = ContextStrategies.Full,
        string scene = SessionSceneTypes.ProjectBrainstorm,
        IReadOnlyList<ConversationMention>? mentions = null,
        string? rawInput = null)
    {
        var task = await StartSessionTaskCoreAsync(projectId, input, contextStrategy, scene, mentions, rawInput);
        return CreateConversationTaskOutput(
            task.Session,
            task.TaskId,
            task.Status,
            task.EntryKind,
            isAwaitingInput: task.IsAwaitingInput);
    }

    private async Task<SessionTaskHandle> StartSessionTaskCoreAsync(
        Guid projectId,
        string input,
        string contextStrategy,
        string scene,
        IReadOnlyList<ConversationMention>? mentions = null,
        string? rawInput = null)
    {
        var normalizedScene = NormalizeScene(scene);
        ChatSession session;

        using (var persistence = CreatePersistenceScope())
        {
            var existing = await persistence.ChatSessions
                .AsNoTracking()
                .Where(s => s.ProjectId == projectId
                    && s.Scene == normalizedScene
                    && (s.Status == SessionStatus.Active || s.Status == SessionStatus.AwaitingInput))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                // zh-CN: 同一项目场景只保留一个活跃会话，新的输入直接追加到现有链路。
                // en: Keep only one active session per project scene; new input is appended to the existing chain.
                return await SendMessageTaskCoreAsync(existing.Id, input, mentions, rawInput);
            }

            session = new ChatSession
            {
                ProjectId = projectId,
                InitialInput = rawInput ?? input,
                ContextStrategy = contextStrategy,
                Scene = normalizedScene
            };

            persistence.ChatSessions.Add(session);
            await persistence.RepositoryContext.SaveChangesAsync();
        }

        _streamManager.Create(session.Id);

        var sessionCts = new CancellationTokenSource();
        _sessionCts[session.Id] = (sessionCts, DateTime.UtcNow);
        var executionPackageId = await CreateExecutionPackageAsync(
            session,
            rawInput ?? input,
            ResolveEntryKind(session.Scene),
            initiatorRole: MessageRoles.User,
            targetRole: ResolveDefaultTargetRole(session.Scene),
            ct: CancellationToken.None);

        await PushEventAsync(session.Id, SessionEventTypes.SessionCreated, payload: new
        {
            sessionId = session.Id,
            projectId,
            input = rawInput ?? input,
            scene = session.Scene
        }, executionPackageId: executionPackageId);

        var rootMessageId = Guid.NewGuid();
        await PushEventAsync(session.Id, SessionEventTypes.UserInput, messageId: rootMessageId, payload: new
        {
            messageId = rootMessageId,
            parentMessageId = (Guid?)null,
            role = MessageRoles.User,
            content = rawInput ?? input,
            input
        }, executionPackageId: executionPackageId);

        await _notification.NotifyAsync(Channels.Project(projectId), "session_started", new
        {
            sessionId = session.Id,
            input = Truncate(rawInput ?? input, 100)
        });

        // zh-CN: 首轮执行转入后台，HTTP 创建接口只负责返回可订阅的会话标识。
        // en: The first execution turn runs in the background so the HTTP create endpoint only needs to return a subscribable session id.
        _ = Task.Run(() => ExecuteUserInputAsync(session, input, rawInput ?? input, mentions, sessionCts.Token, rootMessageId, null, executionPackageId));

        return new SessionTaskHandle(
            session,
            executionPackageId,
            ExecutionPackageStatus.Active,
            ResolveEntryKind(session.Scene),
            IsAwaitingInput: false);
    }

    /// <summary>
    /// 向现有会话追加消息，必要时恢复暂停链路。
    /// Appends a message to an existing session and resumes a paused chain when needed.
    /// </summary>
    public async Task SendMessageAsync(Guid sessionId, string input, IReadOnlyList<ConversationMention>? mentions = null, string? rawInput = null)
    {
        _ = await SendMessageTaskCoreAsync(sessionId, input, mentions, rawInput);
    }

    /// <summary>
    /// 向现有会话追加消息，并返回当前这一轮输入对应的统一任务引用。
    /// Appends a message to an existing session and returns the unified task reference for the current turn.
    /// </summary>
    public async Task<ConversationTaskOutput> SendMessageTaskAsync(Guid sessionId, string input, IReadOnlyList<ConversationMention>? mentions = null, string? rawInput = null)
    {
        var task = await SendMessageTaskCoreAsync(sessionId, input, mentions, rawInput);
        return CreateConversationTaskOutput(
            task.Session,
            task.TaskId,
            task.Status,
            task.EntryKind,
            isAwaitingInput: task.IsAwaitingInput);
    }

    private async Task<SessionTaskHandle> SendMessageTaskCoreAsync(
        Guid sessionId,
        string input,
        IReadOnlyList<ConversationMention>? mentions = null,
        string? rawInput = null,
        ChatSession? loadedSession = null)
    {
        // zh-CN: 先尝试恢复“等待用户输入”的链路；只有未暂停时才新建根帧执行。
        // en: Resume an awaiting-input chain first; only create a new root frame when the session is not paused.
        if (_awaitingInput.TryRemove(sessionId, out var tcs))
        {
            _logger.LogInformation("Session {SessionId} resumed with user input", sessionId);

            // zh-CN: 恢复时先把数据库状态切回 Active，确保其他读取接口看到一致状态。
            // en: Switch the persisted state back to Active before resuming so readers observe a consistent status.
            using var persistence = CreatePersistenceScope();
            var session = loadedSession ?? await persistence.ChatSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.Status = SessionStatus.Active;
                await persistence.RepositoryContext.SaveChangesAsync();
            }

            if (session == null)
                throw new KeyNotFoundException($"Session '{sessionId}' was not found.");

            var currentFrameId = await FindCurrentFrameIdAsync(sessionId);
            var resumedExecutionPackageId = await GetFrameExecutionPackageIdAsync(currentFrameId, CancellationToken.None);
            if (!resumedExecutionPackageId.HasValue)
            {
                // zh-CN: 理论上 awaiting-input 总会挂在已有执行包上；这里只做保底，避免异常状态导致前端拿不到可订阅的任务标识。
                // en: Awaiting-input should normally resume an existing package; this fallback prevents broken state from returning no subscribable task id.
                resumedExecutionPackageId = await CreateExecutionPackageAsync(
                    session,
                    rawInput ?? input,
                    ResolveEntryKind(session.Scene),
                    initiatorRole: MessageRoles.User,
                    targetRole: ResolveDefaultTargetRole(session.Scene),
                    ct: CancellationToken.None);
            }
            if (resumedExecutionPackageId.HasValue)
            {
                await UpdateExecutionPackageStatusAsync(
                    resumedExecutionPackageId.Value,
                    ExecutionPackageStatus.Active,
                    complete: false,
                    ct: CancellationToken.None);
            }
            var parentMessageId = await GetLatestVisibleMessageIdAsync(sessionId);
            var resumedMessageId = Guid.NewGuid();
            if (currentFrameId.HasValue)
            {
                await AppendMessageAsync(
                    sessionId,
                    currentFrameId.Value,
                    MessageRoles.User,
                    agentRole: null,
                    rawInput ?? input,
                    MessageContentTypes.Text,
                    parentMessageId,
                    CancellationToken.None,
                    resumedMessageId,
                    executionPackageId: resumedExecutionPackageId,
                    originatingFrameId: currentFrameId);
            }

            await PushEventAsync(sessionId, SessionEventTypes.UserInput, currentFrameId, payload: new
            {
                messageId = resumedMessageId,
                parentMessageId,
                role = MessageRoles.User,
                content = rawInput ?? input,
                input
            }, messageId: resumedMessageId, executionPackageId: resumedExecutionPackageId);

            await PushEventAsync(sessionId, SessionEventTypes.ResumedByUser, currentFrameId, payload: new
            {
                messageId = resumedMessageId,
                parentMessageId,
                content = rawInput ?? input,
                input
            }, messageId: resumedMessageId, executionPackageId: resumedExecutionPackageId);

            // zh-CN: TaskCompletionSource 作为暂停点，设置结果即可让原链路继续执行。
            // en: The TaskCompletionSource acts as the suspension point; completing it lets the original chain continue.
            tcs.TrySetResult(input);
            return new SessionTaskHandle(
                session,
                resumedExecutionPackageId.Value,
                ExecutionPackageStatus.Active,
                await GetExecutionPackageEntryKindAsync(resumedExecutionPackageId, CancellationToken.None)
                    ?? ResolveEntryKind(session.Scene),
                IsAwaitingInput: false);
        }

        // zh-CN: 未暂停时把追加消息视作一轮新的根帧执行，但仍复用同一个长期 Session。
        // en: When not paused, treat the message as a new root-frame turn while still reusing the long-lived session.
        ChatSession? existing;
        using (var persistence = CreatePersistenceScope())
        {
            existing = loadedSession ?? await persistence.ChatSessions.FindAsync(sessionId);
        }

        if (existing == null)
        {
            _logger.LogWarning("Session {SessionId} not found", sessionId);
            throw new KeyNotFoundException($"Session '{sessionId}' was not found.");
        }

        // zh-CN: 历史会话重新激活时要补建内存流，以继续支持实时订阅。
        // en: Reactivated sessions may need a fresh in-memory stream so real-time subscribers can continue receiving events.
        if (!_streamManager.IsActive(sessionId))
        {
            _streamManager.Create(sessionId);
        }

        // zh-CN: 重新进入执行链路时补齐 Session 级取消令牌，避免后续取消操作失效。
        // en: Recreate the session-level cancellation token when execution resumes so later cancellation still works.
        if (!_sessionCts.ContainsKey(sessionId))
        {
            var cts = new CancellationTokenSource();
            _sessionCts[sessionId] = (cts, DateTime.UtcNow);
        }

        var sessionCts = _sessionCts[sessionId].cts;
        var parentId = await GetLatestVisibleMessageIdAsync(sessionId);
        var rootMessageId = Guid.NewGuid();
        var executionPackageId = await CreateExecutionPackageAsync(
            existing,
            rawInput ?? input,
            ResolveEntryKind(existing.Scene),
            initiatorRole: MessageRoles.User,
            targetRole: ResolveDefaultTargetRole(existing.Scene),
            ct: CancellationToken.None);

        // zh-CN: 即使随后执行失败，也先把用户输入事件投影出去，保证前端对话视图完整。
        // en: Publish the user-input event before execution so the frontend conversation stays complete even if processing later fails.
        await PushEventAsync(sessionId, SessionEventTypes.UserInput, payload: new
        {
            messageId = rootMessageId,
            parentMessageId = parentId,
            role = MessageRoles.User,
            content = rawInput ?? input,
            input
        }, messageId: rootMessageId, executionPackageId: executionPackageId);

        // zh-CN: 后台执行避免消息发送接口被长时间阻塞。
        // en: Execute in the background so the send-message endpoint does not block on a long-running turn.
        _ = Task.Run(() => ExecuteUserInputAsync(existing, input, rawInput ?? input, mentions, sessionCts.Token, rootMessageId, parentId, executionPackageId));

        return new SessionTaskHandle(
            existing,
            executionPackageId,
            ExecutionPackageStatus.Active,
            ResolveEntryKind(existing.Scene),
            IsAwaitingInput: false);
    }

    /// <summary>
    /// 手动恢复被阻塞的 ProjectGroup 任务。
    /// Manually resumes a blocked ProjectGroup task.
    /// </summary>
    public async Task<bool> ResumeBlockedProjectGroupTaskAsync(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        var resume = await _projectGroupExecution.ResumeBlockedTaskAsync(projectId, taskId, ct);
        if (resume == null)
            return false;

        await ExecuteProjectGroupLeaseAsync(resume.Lease, ct);
        return true;
    }

    public async Task ExecuteProjectGroupDispatchesAsync(
        Guid sessionId,
        Guid frameId,
        Guid? parentMessageId,
        IReadOnlyList<ProjectGroupDispatchTarget> dispatchTargets,
        CancellationToken ct = default)
    {
        if (dispatchTargets.Count == 0)
            return;

        using var persistence = CreatePersistenceScope();
        var session = await persistence.ChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sessionId, ct)
            ?? throw new InvalidOperationException($"Session {sessionId} not found");
        var frame = await persistence.ChatFrames
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == frameId, ct)
            ?? throw new InvalidOperationException($"Frame {frameId} not found");

        await ExecuteProjectGroupDispatchPlanAsync(
            session,
            frame,
            new ProjectGroupDispatchPlan(string.Empty, dispatchTargets),
            parentMessageId,
            ct);
    }

    /// <summary>
    /// 执行用户输入 — 创建根 Frame 并执行链式流转
    /// </summary>
    private async Task ExecuteUserInputAsync(
        ChatSession session,
        string input,
        string displayInput,
        IReadOnlyList<ConversationMention>? mentions,
        CancellationToken sessionCt,
        Guid rootMessageId,
        Guid? parentMessageId,
        Guid executionPackageId)
    {
        try
        {
            var scene = ParseScene(session.Scene);
            await _orchestration.InitializeProjectAgentsAsync(session.ProjectId, sessionCt);

            if (scene == SceneType.ProjectGroup
                && await TryHandleProjectGroupUserInputAsync(session, input, displayInput, mentions, sessionCt, rootMessageId, parentMessageId, executionPackageId))
            {
                return;
            }

            // 当前运行时仅保留秘书作为默认入口角色。
            var rootFrame = await CreateFrameAsync(
                session.Id,
                executionPackageId,
                parentFrameId: null,
                depth: 0,
                initiatorRole: "user",
                targetRole: BuiltinRoleTypes.Secretary,
                purpose: input,
                ct: sessionCt,
                userMessageContent: displayInput,
                parentMessageId: parentMessageId,
                messageId: rootMessageId);

            await ExecuteFrameAsync(session, rootFrame, scene, sessionCt);
            await UpdateExecutionPackageStatusAsync(executionPackageId, ExecutionPackageStatus.Completed, complete: true, ct: CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Session {SessionId} execution cancelled", session.Id);
            await UpdateExecutionPackageStatusAsync(executionPackageId, ExecutionPackageStatus.Cancelled, complete: true, ct: CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} execution failed", session.Id);
            await PushEventAsync(session.Id, SessionEventTypes.Error, payload: new { message = ex.Message }, executionPackageId: executionPackageId);
            await UpdateExecutionPackageStatusAsync(executionPackageId, ExecutionPackageStatus.Failed, complete: true, ct: CancellationToken.None);
        }
    }

    /// <summary>
    /// 取消整个会话。
    /// Cancels the entire session.
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

        using var persistence = CreatePersistenceScope();
        var session = await persistence.ChatSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.Status = SessionStatus.Cancelled;
            session.CompletedAt = DateTime.UtcNow;
            await persistence.RepositoryContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 弹出当前帧，仅取消当前栈顶执行。
    /// Pops the current frame and cancels only the active stack-top execution.
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
    /// 判断会话是否正等待用户输入。
    /// Determines whether a session is currently awaiting user input.
    /// </summary>
    public bool IsAwaitingInput(Guid sessionId) => _awaitingInput.ContainsKey(sessionId);

    /// <summary>
    /// 执行单个 Frame — 调用目标 Agent，处理路由和子 Frame
    /// </summary>
    private async Task<FrameExecutionResult> ExecuteFrameAsync(
        ChatSession session,
        ChatFrame frame,
        SceneType? scene,
        CancellationToken sessionCt,
        string? forcedTargetRole = null,
        bool? forcedSecretaryTarget = null,
        string? forcedDispatchSource = null)
    {
        using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(sessionCt);
        _frameCts[frame.Id] = (frameCts, DateTime.UtcNow);
        _currentFrame[session.Id] = frame.Id;

        try
        {
            var ct = frameCts.Token;
            var isSecretaryTarget = forcedSecretaryTarget ?? await IsSecretaryTargetAsync(frame, ct);
            var currentTargetRole = !string.IsNullOrWhiteSpace(forcedTargetRole)
                ? forcedTargetRole
                : await ResolveFrameTargetDisplayAsync(frame, ct);

            await PushEventAsync(session.Id, SessionEventTypes.FramePushed, frame.Id, new
            {
                frameId = frame.Id,
                depth = frame.Depth,
                initiatorAgentRoleId = frame.InitiatorAgentRoleId,
                initiatorProjectAgentRoleId = frame.InitiatorProjectAgentRoleId,
                targetAgentRoleId = frame.TargetAgentRoleId,
                targetProjectAgentRoleId = frame.TargetProjectAgentRoleId,
                purpose = Truncate(frame.Purpose, 200)
            });

            // 发送思考事件
            await PushEventAsync(session.Id, SessionEventTypes.Thought, frame.Id, new
            {
                agentRoleId = frame.TargetAgentRoleId,
                projectAgentRoleId = frame.TargetProjectAgentRoleId,
                message = $"正在处理: {Truncate(frame.Purpose, 100)}"
            });

            // 调用目标 Agent
            var response = await ExecuteRuntimeFrameAsync(session, frame, scene, ct, currentTargetRole, forcedDispatchSource);

            var responseContent = response.Content ?? "";
            if (scene == SceneType.ProjectBrainstorm
                && isSecretaryTarget
                && response.Success)
            {
                var brainstormState = await ApplyProjectBrainstormStateAsync(session.ProjectId, responseContent, ct);
                responseContent = brainstormState.DisplayContent;

                if (brainstormState.DocumentUpdated || brainstormState.PhaseChanged)
                {
                    await PushEventAsync(session.Id, SessionEventTypes.ProjectStateChanged, frame.Id, new
                    {
                        phase = brainstormState.CurrentPhase,
                        documentUpdated = brainstormState.DocumentUpdated,
                        phaseChanged = brainstormState.PhaseChanged
                    });
                }
            }

            ProjectGroupDispatchPlan? dispatchPlan = null;
            ProjectGroupCapabilityPlan? capabilityPlan = null;
            ProjectGroupOrchestratorRelayPlan? relayPlan = null;
            if (scene == SceneType.ProjectGroup && response.Success)
            {
                if (isSecretaryTarget)
                {
                    var orchestratorResult = await BuildProjectGroupOrchestratorResultAsync(
                        session,
                        responseContent,
                        ct);
                    if (orchestratorResult != null)
                    {
                        dispatchPlan = orchestratorResult.DispatchPlan;
                        responseContent = orchestratorResult.VisibleReply;
                    }
                    else
                    {
                        dispatchPlan = await BuildProjectGroupDispatchPlanAsync(
                            session,
                            responseContent,
                            "project_group_secretary_dispatch",
                            ct);
                        if (dispatchPlan != null)
                        {
                            responseContent = dispatchPlan.DisplayContent;
                        }
                    }
                }
                else if (_projectGroupExecution.TryExtractDispatch(responseContent, out var memberDisplayContent, out var memberDispatches))
                {
                    responseContent = string.IsNullOrWhiteSpace(memberDisplayContent)
                        ? "我已完成当前阶段，已请求项目模型继续编排后续协作。"
                        : memberDisplayContent;
                    relayPlan = BuildProjectGroupOrchestratorRelayPlan(
                        currentTargetRole,
                        memberDisplayContent,
                        memberDispatches);
                }
                else if (_projectGroupExecution.TryExtractCapabilityRequest(responseContent, out var capabilityDisplayContent, out var capabilityRequest)
                    && capabilityRequest != null)
                {
                    // zh-CN: 成员角色返回能力申请时，先把展示文案留给用户，再生成后续审批/重试计划。
                    // en: When a member role asks for extra capability, keep the display text for the user and then build the approval/retry plan.
                    responseContent = string.IsNullOrWhiteSpace(capabilityDisplayContent)
                        ? "我当前缺少继续执行所需的能力，已向秘书申请支持。"
                        : capabilityDisplayContent;
                    capabilityPlan = await BuildCapabilityPlanAsync(
                        session.ProjectId,
                        frame.Id,
                        currentTargetRole,
                        capabilityRequest,
                        ct);
                }
            }

            var shouldPublishVisibleResponse = !(scene == SceneType.ProjectGroup
                && isSecretaryTarget
                && dispatchPlan != null
                && string.IsNullOrWhiteSpace(responseContent));
            Entities.ChatMessage? responseMessage = null;
            if (shouldPublishVisibleResponse)
            {
                responseMessage = await PublishAgentMessageAsync(
                    session.Id,
                    frame,
                    currentTargetRole,
                    responseContent,
                    response.Success,
                    ct,
                    usage: response.Usage,
                    timing: response.Timing,
                    model: response.Model);
            }
            var responseParentMessageId = responseMessage?.Id ?? await GetFrameEntryMessageIdAsync(frame.Id, ct);

            // 检查是否需要用户输入（暂停链式流转）
            if (response.RequiresUserInput)
            {
                await PauseForUserInputAsync(session, frame, ct);
                // 暂停恢复后，不再继续当前链路（用户新消息会触发新的 ExecuteUserInputAsync）
                await CompleteFrameAsync(frame.Id, responseContent, ct);
                return new FrameExecutionResult(responseContent, response.Success, ShouldRetry: false, SecretaryNotice: null, response.RequiresUserInput);
            }

            // 检查是否需要路由到下一个 Agent（链式流转）
            if (scene != SceneType.ProjectGroup
                && !string.IsNullOrEmpty(response.TargetRole)
                && !string.Equals(response.TargetRole, currentTargetRole, StringComparison.OrdinalIgnoreCase))
            {
                await PushEventAsync(session.Id, SessionEventTypes.Routing, frame.Id, new
                {
                    from = currentTargetRole,
                    to = response.TargetRole,
                    reason = "Agent routing marker"
                });

                var childFrame = await CreateFrameAsync(
                    session.Id, frame.ExecutionPackageId, frame.Id, frame.Depth + 1,
                    currentTargetRole,
                    response.TargetRole,
                    responseContent,
                    ct,
                    parentMessageId: responseParentMessageId);

                var childResult = await ExecuteFrameAsync(session, childFrame, scene, sessionCt);

                await PushEventAsync(session.Id, SessionEventTypes.FrameCompleted, childFrame.Id, new
                {
                    frameId = childFrame.Id,
                    result = Truncate(childResult.Content, 200),
                    status = childResult.Success ? "success" : "failure"
                });

                return childResult;
            }

            // 当前 Frame 完成
            await CompleteFrameAsync(frame.Id, responseContent, ct);

            await PushEventAsync(session.Id, SessionEventTypes.FrameCompleted, frame.Id, new
            {
                frameId = frame.Id,
                result = Truncate(responseContent, 200),
                status = response.Success ? "success" : "failure"
            });

            if (dispatchPlan != null)
            {
                await ExecuteProjectGroupDispatchPlanAsync(session, frame, dispatchPlan, responseParentMessageId, sessionCt);
            }
            else if (relayPlan != null)
            {
                await ExecuteProjectOrchestratorRelayAsync(session, frame, relayPlan, responseParentMessageId, sessionCt);
            }

            return new FrameExecutionResult(
                responseContent,
                response.Success && capabilityPlan == null,
                ShouldRetry: capabilityPlan?.RetryWithoutPenalty ?? false,
                SecretaryNotice: capabilityPlan?.SecretaryNotice,
                RetryWithoutPenalty: capabilityPlan?.RetryWithoutPenalty ?? false,
                RequiresUserInput: response.RequiresUserInput);
        }
        catch (OperationCanceledException) when (frameCts.IsCancellationRequested && !sessionCt.IsCancellationRequested)
        {
            await CompleteFrameAsync(frame.Id, "Frame popped by user", CancellationToken.None, FrameStatus.Popped);

            await PushEventAsync(session.Id, SessionEventTypes.FramePopped, frame.Id, new
            {
                frameId = frame.Id,
                reason = "User requested pop"
            });

            return new FrameExecutionResult("Frame popped by user", Success: false, ShouldRetry: false);
        }
        finally
        {
            _frameCts.TryRemove(frame.Id, out _);
            _currentFrame.TryRemove(session.Id, out _);
        }
    }

    /// <summary>
    /// 尝试将会话场景字符串解析为运行时枚举；未知值返回 <see langword="null"/>，让调用方决定默认场景。
    /// Attempts to parse the stored session scene into the runtime enum; unknown values return <see langword="null"/> so the caller can choose the fallback.
    /// </summary>
    private static SceneType? ParseScene(string? scene) =>
        SessionSceneTypes.TryParse(scene, out var parsed) ? parsed : null;

    private static Dictionary<string, string> BuildRuntimeExtra(
        string originalInput,
        string executionPurpose,
        string? targetRole,
        string? initiatorRole,
        string? dispatchSource,
        string? dispatchContext)
    {
        var extra = new Dictionary<string, string>
        {
            ["skip_final_projection"] = "true",
            ["openstaff_original_input"] = originalInput,
            ["openstaff_execution_purpose"] = executionPurpose
        };

        if (!string.IsNullOrWhiteSpace(targetRole))
        {
            extra["openstaff_target_role"] = targetRole;
        }

        if (!string.IsNullOrWhiteSpace(initiatorRole))
        {
            extra["openstaff_initiator_role"] = initiatorRole;
        }

        if (!string.IsNullOrWhiteSpace(dispatchSource))
        {
            extra["openstaff_dispatch_source"] = dispatchSource;
        }

        if (!string.IsNullOrWhiteSpace(dispatchContext))
        {
            extra["openstaff_dispatch_context"] = dispatchContext;
        }

        return extra;
    }

    private async Task<(string? DispatchSource, string? DispatchContext)> ResolveFrameDispatchContextAsync(Guid? taskId, CancellationToken ct)
    {
        if (!taskId.HasValue)
            return (null, null);

        using var persistence = CreatePersistenceScope();
        var metadataJson = await persistence.Tasks
            .AsNoTracking()
            .Where(task => task.Id == taskId.Value)
            .Select(task => task.Metadata)
            .FirstOrDefaultAsync(ct);
        var dispatchSource = TaskItemRuntimeMetadata.TryParse(metadataJson)?.Source;
        return (dispatchSource, DescribeProjectGroupDispatchContext(dispatchSource));
    }

    private async Task<TaskItemRuntimeMetadata?> ReadFrameTaskMetadataAsync(Guid? taskId, CancellationToken ct)
    {
        if (!taskId.HasValue)
            return null;

        using var persistence = CreatePersistenceScope();
        var metadataJson = await persistence.Tasks
            .AsNoTracking()
            .Where(task => task.Id == taskId.Value)
            .Select(task => task.Metadata)
            .FirstOrDefaultAsync(ct);
        return TaskItemRuntimeMetadata.TryParse(metadataJson);
    }

    private static ChatRole ResolveRuntimeInputRole(Entities.ChatMessage entryMessage)
    {
        // ProjectGroup 内部 dispatch 帧会把入口消息持久化成 assistant/internal，便于 UI 回放保留“谁在分发任务”。
        // 但对目标成员来说，这条内容本质上仍然是新的任务指令，应作为用户输入送给模型。
        // 否则部分严格的 OpenAI-compatible 提供商（例如 GLM）会拒绝当前输入是 assistant 角色的请求。
        if (string.Equals(entryMessage.ContentType, MessageContentTypes.Internal, StringComparison.OrdinalIgnoreCase))
            return ChatRole.User;

        return ToChatRole(entryMessage.Role);
    }

    private static string? DescribeProjectGroupDispatchContext(string? dispatchSource) => dispatchSource switch
    {
        "project_group_user_input" => "This is a new user request in the project group. You are the hidden project orchestrator and should decide who, if anyone, should reply in the visible group chat.",
        "project_group_mention" => "The user mentioned you directly in the project group, so this task should be handled by you first.",
        "project_group_secretary_dispatch" => "The hidden project orchestrator reviewed the group context and assigned the next step to you.",
        "project_group_secretary_broadcast" => "The secretary broadcast the same collaboration task to multiple members, and you are one of them.",
        "project_group_member_dispatch" => "Another project member finished the current stage and handed the follow-up work to you.",
        "project_group_member_broadcast" => "Another project member broadcast the follow-up collaboration task to multiple members, and you are one of them.",
        "project_group_member_replan" => "Another project member reported progress and asked the hidden project orchestrator to re-plan the next collaboration step and choose who should speak next.",
        "project_group_system_kickoff" => "The project has just entered execution, and the system selected you for the kickoff confirmation or first action.",
        _ => null
    };

    /// <summary>
    /// 根据帧入口消息重建一次运行时请求并等待处理完成；若处理器缺失会抛错，并始终在 <c>finally</c> 中移除消息处理器。
    /// Rehydrates a runtime request from the frame's entry message and waits for completion; it throws when the handler is missing and always removes the handler in the <c>finally</c> block.
    /// </summary>
    private async Task<OrchestrationResponse> ExecuteRuntimeFrameAsync(
        ChatSession session,
        ChatFrame frame,
        SceneType? scene,
        CancellationToken ct,
        string? forcedTargetRole = null,
        string? forcedDispatchSource = null)
    {
        var entryMessage = await GetFrameEntryMessageAsync(frame.Id, ct)
            ?? throw new InvalidOperationException($"Frame '{frame.Id}' entry message was not found.");
        var projectAgentId = await ResolveFrameProjectAgentIdAsync(frame.TaskId, ct);
        var taskMetadata = await ReadFrameTaskMetadataAsync(frame.TaskId, ct);
        var frameTargetRole = !string.IsNullOrWhiteSpace(forcedTargetRole)
            ? forcedTargetRole
            : await ResolveFrameTargetDisplayAsync(frame, ct);
        if (string.IsNullOrWhiteSpace(forcedTargetRole)
            && scene == SceneType.ProjectGroup
            && !string.IsNullOrWhiteSpace(taskMetadata?.TargetRoleName))
        {
            frameTargetRole = taskMetadata!.TargetRoleName!;
        }
        else if (string.IsNullOrWhiteSpace(forcedTargetRole)
            && scene == SceneType.ProjectGroup
            && string.Equals(frameTargetRole, BuiltinRoleTypes.Secretary, StringComparison.OrdinalIgnoreCase)
            && projectAgentId.HasValue)
        {
            frameTargetRole = await ResolveRoleDisplayAsync(projectAgentId.Value, null, ct) ?? frameTargetRole;
        }
        var frameInitiatorRole = await ResolveFrameInitiatorDisplayAsync(frame, ct);
        var dispatchContext = await ResolveFrameDispatchContextAsync(frame.TaskId, ct);
        if (!string.IsNullOrWhiteSpace(forcedDispatchSource))
        {
            dispatchContext = (forcedDispatchSource, DescribeProjectGroupDispatchContext(forcedDispatchSource));
        }

        if (scene == SceneType.ProjectGroup
            && string.Equals(frameTargetRole, BuiltinRoleTypes.Secretary, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(dispatchContext.DispatchSource))
        {
            var derivedSource = string.Equals(entryMessage.Role, MessageRoles.Assistant, StringComparison.OrdinalIgnoreCase)
                ? "project_group_member_replan"
                : "project_group_user_input";
            dispatchContext = (derivedSource, DescribeProjectGroupDispatchContext(derivedSource));
        }
        var runtimeExtra = BuildRuntimeExtra(
            entryMessage.Content,
            frame.Purpose,
            frameTargetRole,
            frameInitiatorRole,
            dispatchContext.DispatchSource,
            dispatchContext.DispatchContext);
        var runtimeInputRole = ResolveRuntimeInputRole(entryMessage);

        var response = await _agentService.CreateMessageAsync(
            new CreateMessageRequest(
                Scene: ToMessageScene(scene),
                MessageContext: new MessageContext(
                    ProjectId: session.ProjectId,
                    SessionId: session.Id,
                    ParentMessageId: entryMessage.ParentMessageId,
                    FrameId: frame.Id,
                    ParentFrameId: frame.ParentFrameId,
                    TaskId: frame.TaskId,
                    ProjectAgentRoleId: projectAgentId,
                    TargetRole: frameTargetRole,
                    InitiatorRole: frameInitiatorRole,
                    Extra: runtimeExtra)
                    {
                        ExecutionPackageId = frame.ExecutionPackageId,
                        EntryKind = await GetExecutionPackageEntryKindAsync(frame.ExecutionPackageId, ct),
                        SourceFrameId = frame.Id
                    },
                InputRole: runtimeInputRole,
                Input: frame.Purpose),
            ct);

        if (!_agentService.TryGetMessageHandler(response.MessageId, out var handler) || handler == null)
            throw new InvalidOperationException($"Message handler '{response.MessageId}' was not created.");

        try
        {
            var summary = await handler.Completion;
            return new OrchestrationResponse
            {
                Success = summary.Success,
                Content = summary.Success || !string.IsNullOrWhiteSpace(summary.Content)
                    ? summary.Content
                    : $"处理失败: {summary.Error}",
                Usage = summary.Usage == null
                    ? null
                    : new OrchestrationUsage
                    {
                        InputTokens = summary.Usage.InputTokens,
                        OutputTokens = summary.Usage.OutputTokens,
                        TotalTokens = summary.Usage.TotalTokens
                    },
                Timing = summary.Timing == null
                    ? null
                    : new OrchestrationTiming
                    {
                        TotalMs = summary.Timing.TotalMs,
                        FirstTokenMs = summary.Timing.FirstTokenMs
                    },
                Model = summary.Model,
                Errors = string.IsNullOrWhiteSpace(summary.Error) ? [] : [summary.Error]
            };
        }
        finally
        {
            _agentService.RemoveMessageHandler(response.MessageId);
        }
    }

    /// <summary>
    /// 将场景字符串规范为已知枚举名称；未知值统一回退到 <c>ProjectBrainstorm</c>，保证新建和续写会话使用同一键值。
    /// Normalizes a scene string to a known enum name; unknown values fall back to <c>ProjectBrainstorm</c> so new and resumed sessions use the same key.
    /// </summary>
    private static string NormalizeScene(string? scene)
    {
        if (SessionSceneTypes.TryParse(scene, out var parsed))
        {
            return parsed.ToString();
        }

        return SessionSceneTypes.ProjectBrainstorm;
    }

    /// <summary>
    /// 尝试处理项目群用户输入；所有新输入都会先进入隐藏项目编排器，再由统一 contract 决定可见回复与后续派工。
    /// Handles project-group user input by sending every new message to the hidden project orchestrator first, which then decides visible replies and follow-up dispatch via the unified contract.
    /// </summary>
    private async Task<bool> TryHandleProjectGroupUserInputAsync(
        ChatSession session,
        string input,
        string displayInput,
        IReadOnlyList<ConversationMention>? mentions,
        CancellationToken sessionCt,
        Guid rootMessageId,
        Guid? parentMessageId,
        Guid executionPackageId)
    {
        var dispatch = (await _projectGroupExecution.ResolveDispatchTargetAsync(session.ProjectId, input, mentions, sessionCt))
            with { UserMessageContent = displayInput };

        var rootFrame = await CreateFrameAsync(
            session.Id,
            executionPackageId,
            parentFrameId: null,
            depth: 0,
            initiatorRole: "user",
            targetRole: dispatch.TargetRole,
            purpose: dispatch.Purpose,
            ct: sessionCt,
            userMessageContent: dispatch.UserMessageContent,
            parentMessageId: parentMessageId,
            messageId: rootMessageId);

        if (!string.IsNullOrWhiteSpace(dispatch.UnresolvedMessage))
        {
            await PublishAgentMessageAsync(session.Id, rootFrame, BuiltinRoleTypes.Secretary, dispatch.UnresolvedMessage, success: false, sessionCt);
            await CompleteFrameAsync(rootFrame.Id, dispatch.UnresolvedMessage, sessionCt);
            await UpdateExecutionPackageStatusAsync(executionPackageId, ExecutionPackageStatus.Completed, complete: true, ct: CancellationToken.None);
            return true;
        }

        await ExecuteFrameAsync(session, rootFrame, SceneType.ProjectGroup, sessionCt);
        await UpdateExecutionPackageStatusAsync(executionPackageId, ExecutionPackageStatus.Completed, complete: true, ct: CancellationToken.None);
        return true;
    }

    private async Task<ProjectGroupOrchestratorExecutionResult?> BuildProjectGroupOrchestratorResultAsync(
        ChatSession session,
        string responseContent,
        CancellationToken ct)
    {
        if (!_projectGroupExecution.TryExtractOrchestratorResult(responseContent, out var result) || result == null)
        {
            return null;
        }

        ProjectGroupDispatchPlan? dispatchPlan = null;
        if (result.Dispatches.Count > 0)
        {
            var dispatches = result.Dispatches
                .Select(item => new ProjectGroupDispatchInstruction
                {
                    TargetRole = item.TargetRole,
                    Task = item.Task
                })
                .ToArray();
            dispatchPlan = await ResolveProjectGroupDispatchInstructionsAsync(
                session,
                dispatches,
                "project_group_secretary_dispatch",
                result.SecretaryReply ?? string.Empty,
                ct);
        }

        return new ProjectGroupOrchestratorExecutionResult(
            dispatchPlan?.DisplayContent ?? result.SecretaryReply ?? string.Empty,
            dispatchPlan);
    }

    /// <summary>
    /// 解析秘书回复中的旧式分派指令并解析到具体目标；无法解析的项会追加到展示内容中，而不是静默丢弃。
    /// Extracts legacy secretary dispatch instructions and resolves them to concrete targets; unresolved items are appended to the display content instead of being dropped silently.
    /// </summary>
    private async Task<ProjectGroupDispatchPlan?> BuildProjectGroupDispatchPlanAsync(
        ChatSession session,
        string responseContent,
        string dispatchSource,
        CancellationToken ct)
    {
        if (!_projectGroupExecution.TryExtractDispatch(responseContent, out var displayContent, out var dispatches))
        {
            return null;
        }

        return await ResolveProjectGroupDispatchInstructionsAsync(session, dispatches, dispatchSource, displayContent, ct);
    }

    private async Task<ProjectGroupDispatchPlan> ResolveProjectGroupDispatchInstructionsAsync(
        ChatSession session,
        IReadOnlyList<ProjectGroupDispatchInstruction> dispatches,
        string dispatchSource,
        string displayContent,
        CancellationToken ct)
    {
        var resolvedTargets = new List<ProjectGroupDispatchTarget>();
        var unresolvedMessages = new List<string>();

        foreach (var instruction in dispatches)
        {
            var targets = await _projectGroupExecution.ResolveDispatchesAsync(session.ProjectId, instruction, dispatchSource, ct);
            foreach (var target in targets)
            {
                if (!string.IsNullOrWhiteSpace(target.UnresolvedMessage))
                {
                    unresolvedMessages.Add(target.UnresolvedMessage);
                    continue;
                }

                if (target.ProjectAgentRoleId != null)
                {
                    resolvedTargets.Add(target);
                }
            }
        }

        if (unresolvedMessages.Count > 0)
        {
            displayContent = string.Join(
                $"{Environment.NewLine}{Environment.NewLine}",
                new[] { displayContent, string.Join(Environment.NewLine, unresolvedMessages.Select(message => $"- {message}")) }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        return new ProjectGroupDispatchPlan(displayContent, resolvedTargets);
    }

    /// <summary>
    /// 按分派计划创建子帧、发布路由事件并启动各目标任务；可立即执行的租约会并发运行。
    /// Creates child frames, emits routing events, and starts each target task from a dispatch plan; leases that can run now are executed concurrently.
    /// </summary>
    private async Task ExecuteProjectGroupDispatchPlanAsync(
        ChatSession session,
        ChatFrame parentFrame,
        ProjectGroupDispatchPlan plan,
        Guid? parentMessageId,
        CancellationToken sessionCt)
    {
        if (plan.DispatchTargets.Count == 0)
        {
            return;
        }

        var executionTasks = new List<Task>();
        var recoveredLeases = await _projectGroupExecution.RecoverStaleLeasesAsync(
            session.ProjectId,
            plan.DispatchTargets
                .Where(target => target.ProjectAgentRoleId.HasValue)
                .Select(target => target.ProjectAgentRoleId!.Value)
                .Distinct()
                .ToArray(),
            sessionCt);
        foreach (var recoveredLease in recoveredLeases)
        {
            executionTasks.Add(ExecuteProjectGroupLeaseAsync(recoveredLease, sessionCt));
        }

        var parentTargetRole = await ResolveFrameTargetDisplayAsync(parentFrame, sessionCt);
        foreach (var target in plan.DispatchTargets)
        {
            await PushEventAsync(session.Id, SessionEventTypes.Routing, parentFrame.Id, new
            {
                from = parentTargetRole,
                to = target.TargetRole,
                reason = target.DispatchSource
            });

            var childFrame = await CreateFrameAsync(
                session.Id,
                parentFrame.ExecutionPackageId,
                parentFrame.Id,
                parentFrame.Depth + 1,
                parentTargetRole,
                target.TargetRole,
                target.Purpose,
                sessionCt,
                userMessageContent: target.UserMessageContent,
                messageRole: MessageRoles.Assistant,
                messageAgentRole: parentTargetRole,
                messageContentType: MessageContentTypes.Internal,
                parentMessageId: parentMessageId,
                targetProjectAgentRoleIdOverride: target.ProjectAgentRoleId);

            var queueResult = await _projectGroupExecution.QueueTaskAsync(session.ProjectId, session.Id, childFrame.Id, target, sessionCt);
            await PushTaskStateChangedAsync(
                session.Id,
                childFrame.Id,
                queueResult.TaskId,
                queueResult.TargetRole,
                queueResult.Lease == null ? TaskItemStatus.Pending : TaskItemStatus.InProgress);
            if (!string.IsNullOrWhiteSpace(queueResult.QueueAcknowledgement))
            {
                await PublishAgentMessageAsync(session.Id, childFrame, queueResult.TargetRole, queueResult.QueueAcknowledgement, success: true, sessionCt);
                continue;
            }

            if (queueResult.Lease != null)
            {
                executionTasks.Add(ExecuteProjectGroupLeaseAsync(queueResult.Lease, sessionCt));
            }
        }

        if (executionTasks.Count > 0)
        {
            await Task.WhenAll(executionTasks);
        }
    }

    private async Task ExecuteProjectOrchestratorRelayAsync(
        ChatSession session,
        ChatFrame parentFrame,
        ProjectGroupOrchestratorRelayPlan plan,
        Guid? parentMessageId,
        CancellationToken sessionCt)
    {
        await PushEventAsync(session.Id, SessionEventTypes.Routing, parentFrame.Id, new
        {
            from = await ResolveFrameTargetDisplayAsync(parentFrame, sessionCt),
            to = BuiltinRoleTypes.Secretary,
            reason = "project_group_member_replan"
        });

        var parentTargetRole = await ResolveFrameTargetDisplayAsync(parentFrame, sessionCt);
        var childFrame = await CreateFrameAsync(
            session.Id,
            parentFrame.ExecutionPackageId,
            parentFrame.Id,
            parentFrame.Depth + 1,
            parentTargetRole,
            BuiltinRoleTypes.Secretary,
            plan.Purpose,
            sessionCt,
            userMessageContent: plan.Purpose,
            messageRole: MessageRoles.Assistant,
            messageAgentRole: parentTargetRole,
            messageContentType: MessageContentTypes.Internal,
            parentMessageId: parentMessageId);

        await ExecuteFrameAsync(
            session,
            childFrame,
            SceneType.ProjectGroup,
            sessionCt,
            forcedTargetRole: BuiltinRoleTypes.Secretary,
            forcedSecretaryTarget: true,
            forcedDispatchSource: "project_group_member_replan");
    }

    private static ProjectGroupOrchestratorRelayPlan? BuildProjectGroupOrchestratorRelayPlan(
        string? currentTargetRole,
        string displayContent,
        IReadOnlyList<ProjectGroupDispatchInstruction> dispatches)
    {
        var suggestions = dispatches
            .Select(instruction =>
            {
                var target = string.IsNullOrWhiteSpace(instruction.TargetRole)
                    ? null
                    : instruction.TargetRole.Trim();
                var task = string.IsNullOrWhiteSpace(instruction.Task)
                    ? null
                    : instruction.Task.Trim();
                if (string.IsNullOrWhiteSpace(target) && string.IsNullOrWhiteSpace(task))
                {
                    return null;
                }

                return string.IsNullOrWhiteSpace(target)
                    ? $"- {task}"
                    : string.IsNullOrWhiteSpace(task)
                        ? $"- {target}"
                        : $"- {target}: {task}";
            })
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Cast<string>()
            .ToArray();
        if (suggestions.Length == 0)
        {
            return null;
        }

        var roleName = string.IsNullOrWhiteSpace(currentTargetRole) ? "该成员" : currentTargetRole;
        var memberSummary = string.IsNullOrWhiteSpace(displayContent)
            ? "（成员未提供额外自然语言说明）"
            : displayContent.Trim();
        var purpose = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            [
                $"{roleName} 已完成当前阶段，请你作为项目编排内核重新评估后续协作并决定下一步。",
                $"成员反馈：{memberSummary}",
                $"建议后续协作：{Environment.NewLine}{string.Join(Environment.NewLine, suggestions)}"
            ]);

        return new ProjectGroupOrchestratorRelayPlan(purpose);
    }

    /// <summary>
    /// 执行一次项目群任务租约并回写任务状态；它会先重置帧、加载最新实体，并在需要时递归接续下一张租约。
    /// Executes a project-group task lease and reports task-state changes; it first reactivates the frame, loads fresh entities, and recursively continues with the next lease when needed.
    /// </summary>
    private async Task ExecuteProjectGroupLeaseAsync(ProjectGroupExecutionLease lease, CancellationToken sessionCt)
    {
        using var leaseCts = CancellationTokenSource.CreateLinkedTokenSource(sessionCt);
        leaseCts.CancelAfter(_projectGroupLeaseTimeout);

        try
        {
            await ReactivateFrameAsync(lease.FrameId, sessionCt);

            using var persistence = CreatePersistenceScope();
            var session = await persistence.ChatSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == lease.SessionId, sessionCt)
                ?? throw new InvalidOperationException($"Session {lease.SessionId} not found");
            var frame = await persistence.ChatFrames
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == lease.FrameId, sessionCt)
                ?? throw new InvalidOperationException($"Frame {lease.FrameId} not found");
            var leaseTargetAgent = await persistence.ProjectAgentRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == lease.ProjectAgentRoleId, sessionCt);
            frame.TaskId = lease.TaskId;
            frame.TargetProjectAgentRoleId = lease.ProjectAgentRoleId;
            frame.TargetAgentRoleId = leaseTargetAgent?.AgentRoleId;

            await PushTaskStateChangedAsync(session.Id, frame.Id, lease.TaskId, lease.TargetRole, TaskItemStatus.InProgress);
            var result = await ExecuteFrameAsync(
                session,
                frame,
                SceneType.ProjectGroup,
                leaseCts.Token,
                forcedTargetRole: lease.TargetRole,
                forcedSecretaryTarget: false,
                forcedDispatchSource: lease.DispatchSource);
            var completion = await _projectGroupExecution.CompleteTaskAsync(
                lease,
                result.Success,
                result.ShouldRetry,
                result.Content,
                sessionCt,
                result.SecretaryNotice,
                result.RetryWithoutPenalty);
            await PushTaskStateChangedAsync(
                session.Id,
                frame.Id,
                lease.TaskId,
                lease.TargetRole,
                result.Success
                    ? TaskItemStatus.Done
                    : completion.NextLease != null
                        ? TaskItemStatus.InProgress
                        : TaskItemStatus.Blocked);
            if (!string.IsNullOrWhiteSpace(completion.SecretaryNotice))
            {
                await PublishAgentMessageAsync(session.Id, frame, BuiltinRoleTypes.Secretary, completion.SecretaryNotice, success: false, sessionCt);
            }

            if (completion.NextLease != null)
            {
                await ExecuteProjectGroupLeaseAsync(completion.NextLease, sessionCt);
            }
        }
        catch (OperationCanceledException) when (leaseCts.IsCancellationRequested && !sessionCt.IsCancellationRequested)
        {
            _logger.LogWarning(
                "ProjectGroup task {TaskId} for {TargetRole} timed out after {Timeout}",
                lease.TaskId,
                lease.TargetRole,
                _projectGroupLeaseTimeout);
            await FinalizeFailedProjectGroupLeaseAsync(
                lease,
                $"{lease.TargetRole} 处理任务“{lease.TaskTitle}”超时，已停止当前执行并释放后续队列。",
                sessionCt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProjectGroup task {TaskId} for {TargetRole} failed unexpectedly", lease.TaskId, lease.TargetRole);
            await FinalizeFailedProjectGroupLeaseAsync(
                lease,
                $"{lease.TargetRole} 处理任务“{lease.TaskTitle}”时发生异常：{ex.Message}",
                sessionCt);
        }
    }

    private async Task FinalizeFailedProjectGroupLeaseAsync(
        ProjectGroupExecutionLease lease,
        string failureNotice,
        CancellationToken sessionCt)
    {
        var result = string.IsNullOrWhiteSpace(failureNotice)
            ? "ProjectGroup task failed unexpectedly."
            : failureNotice.Trim();
        var completion = await _projectGroupExecution.CompleteTaskAsync(
            lease,
            success: false,
            allowRetry: false,
            result,
            CancellationToken.None,
            secretaryNoticeOverride: result);
        await PushTaskStateChangedAsync(
            lease.SessionId,
            lease.FrameId,
            lease.TaskId,
            lease.TargetRole,
            completion.NextLease != null
                ? TaskItemStatus.InProgress
                : TaskItemStatus.Blocked);
        await CompleteFrameAsync(lease.FrameId, result, CancellationToken.None);

        var frame = await GetFrameAsync(lease.FrameId, CancellationToken.None);
        if (frame != null && !string.IsNullOrWhiteSpace(completion.SecretaryNotice))
        {
            await PublishAgentMessageAsync(
                lease.SessionId,
                frame,
                BuiltinRoleTypes.Secretary,
                completion.SecretaryNotice,
                success: false,
                CancellationToken.None);
        }

        if (completion.NextLease != null)
        {
            await ExecuteProjectGroupLeaseAsync(completion.NextLease, sessionCt);
        }
    }

    /// <summary>
    /// 把已存在的帧恢复为 Active 以便重试；会清空结果和完成时间，缺失帧则静默返回。
    /// Restores an existing frame to Active so it can be retried; it clears the result and completion time, and no-ops when the frame no longer exists.
    /// </summary>
    private async Task ReactivateFrameAsync(Guid frameId, CancellationToken ct)
    {
        using var persistence = CreatePersistenceScope();
        var frame = await persistence.ChatFrames.FindAsync(frameId, ct);
        if (frame == null)
        {
            return;
        }

        frame.Status = FrameStatus.Active;
        frame.Result = null;
        frame.CompletedAt = null;
        await persistence.RepositoryContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 把脑暴输出交给项目服务提取并落库，确保会话生成的阶段状态能反映到项目实体。
    /// Delegates brainstorm output to ProjectService for extraction and persistence so session-generated state is reflected on the project entity.
    /// </summary>
    private async Task<ProjectBrainstormApplyResult> ApplyProjectBrainstormStateAsync(Guid projectId, string responseContent, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
        return await projectService.ApplyBrainstormStateAsync(projectId, responseContent, ct);
    }

    /// <summary>
    /// 暂停会话并等待用户输入。
    /// Pauses the session and waits for additional user input.
    /// </summary>
    private async Task PauseForUserInputAsync(ChatSession session, ChatFrame frame, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _awaitingInput[session.Id] = tcs;

        // zh-CN: 数据库状态要与内存等待点同步更新，否则其他接口会误判会话仍在主动执行。
        // en: Persist the awaiting-input state alongside the in-memory wait point so other APIs do not think the session is still actively executing.
        using (var persistence = CreatePersistenceScope())
        {
            var dbSession = await persistence.ChatSessions.FindAsync(session.Id);
            if (dbSession != null)
            {
                dbSession.Status = SessionStatus.AwaitingInput;
                await persistence.RepositoryContext.SaveChangesAsync();
            }
        }

        if (frame.ExecutionPackageId.HasValue)
        {
            await UpdateExecutionPackageStatusAsync(
                frame.ExecutionPackageId.Value,
                ExecutionPackageStatus.AwaitingInput,
                complete: false,
                ct: CancellationToken.None);
        }

        await PushEventAsync(session.Id, SessionEventTypes.AwaitingInput, frame.Id, new
        {
            agentRoleId = frame.TargetAgentRoleId,
            projectAgentRoleId = frame.TargetProjectAgentRoleId,
            message = "等待用户输入..."
        }, executionPackageId: frame.ExecutionPackageId);

        _logger.LogInformation("Session {SessionId} paused, awaiting user input", session.Id);

        // zh-CN: 这里阻塞的是当前执行链，而不是整个进程；新的用户消息会通过 TaskCompletionSource 恢复它。
        // en: This blocks only the current execution chain, not the whole process; a later user message resumes it through the TaskCompletionSource.
        using var reg = ct.Register(() => tcs.TrySetCanceled());
        await tcs.Task; // 阻塞直到用户回复或取消
    }

    /// <summary>
    /// 创建聊天帧并同步写入入口消息；两者在同一保存周期内落库，确保后续可通过 <c>FrameId</c> 找到执行入口。
    /// Creates a chat frame and writes its entry message in the same save cycle so later execution can reliably recover the frame's starting input through <c>FrameId</c>.
    /// </summary>
    private async Task<ChatFrame> CreateFrameAsync(
        Guid sessionId, Guid? executionPackageId, Guid? parentFrameId, int depth,
        string initiatorRole, string targetRole, string purpose,
        CancellationToken ct,
        string? userMessageContent = null,
        string messageRole = MessageRoles.User,
        string? messageAgentRole = null,
        string messageContentType = MessageContentTypes.Text,
        Guid? parentMessageId = null,
        Guid? messageId = null,
        Guid? targetProjectAgentRoleIdOverride = null)
    {
        using var persistence = CreatePersistenceScope();
        var sessionProjectId = await persistence.ChatSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId)
            .Select(item => item.ProjectId)
            .FirstOrDefaultAsync(ct);
        var initiatorIdentity = await ResolveRoleIdentityAsync(persistence, sessionProjectId, initiatorRole, ct);
        var targetIdentity = targetProjectAgentRoleIdOverride.HasValue
            ? await ResolveProjectRoleIdentityAsync(persistence, targetProjectAgentRoleIdOverride.Value, ct)
            : await ResolveRoleIdentityAsync(persistence, sessionProjectId, targetRole, ct);
        var messageIdentity = await ResolveRoleIdentityAsync(persistence, sessionProjectId, messageAgentRole, ct);

        var frame = new ChatFrame
        {
            SessionId = sessionId,
            ExecutionPackageId = executionPackageId,
            ParentFrameId = parentFrameId,
            Depth = depth,
            InitiatorAgentRoleId = initiatorIdentity.AgentRoleId,
            InitiatorProjectAgentRoleId = initiatorIdentity.ProjectAgentRoleId,
            TargetAgentRoleId = targetIdentity.AgentRoleId,
            TargetProjectAgentRoleId = targetIdentity.ProjectAgentRoleId,
            Purpose = purpose
        };

        persistence.ChatFrames.Add(frame);

        // zh-CN: 帧和入口消息必须同批落库，避免后续根据 FrameId 回溯入口消息时出现悬空引用。
        // en: Persist the frame and its entry message together so later lookups by FrameId never encounter a dangling reference.
        var userMsg = new Entities.ChatMessage
        {
            Id = messageId ?? Guid.NewGuid(),
            FrameId = frame.Id,
            SessionId = sessionId,
            ExecutionPackageId = executionPackageId,
            OriginatingFrameId = frame.Id,
            ParentMessageId = parentMessageId,
            Role = messageRole,
            AgentRoleId = messageIdentity.AgentRoleId,
            ProjectAgentRoleId = messageIdentity.ProjectAgentRoleId,
            Content = userMessageContent ?? purpose,
            ContentType = messageContentType,
            SequenceNo = 0
        };
        persistence.ChatMessages.Add(userMsg);

        if (executionPackageId.HasValue)
        {
            var executionPackage = await persistence.ExecutionPackages.FindAsync(new object[] { executionPackageId.Value }, ct);
            if (executionPackage != null)
            {
                executionPackage.RootFrameId ??= frame.Id;
                executionPackage.TargetAgentRoleId ??= targetIdentity.AgentRoleId;
                executionPackage.TargetProjectAgentRoleId ??= targetIdentity.ProjectAgentRoleId;
                executionPackage.UpdatedAt = DateTime.UtcNow;
            }
        }

        await persistence.RepositoryContext.SaveChangesAsync(ct);
        return frame;
    }

    /// <summary>
    /// 保存智能体回复并向会话流发布消息事件；回复会挂接到帧入口消息下，并携带成功标记与遥测信息。
    /// Persists an agent reply and publishes a session message event; the reply is threaded under the frame entry message and includes success plus telemetry metadata.
    /// </summary>
    private async Task<Entities.ChatMessage> PublishAgentMessageAsync(
        Guid sessionId,
        ChatFrame frame,
        string agentRole,
        string content,
        bool success,
        CancellationToken ct,
        OrchestrationUsage? usage = null,
        OrchestrationTiming? timing = null,
        string? model = null)
    {
        var parentMessageId = await GetFrameEntryMessageIdAsync(frame.Id, ct);
        var savedMessage = await SaveMessageAsync(
            frame,
            MessageRoles.Assistant,
            agentRole,
            content,
            ct,
            parentMessageId: parentMessageId,
            usage: usage,
            timing: timing);

        await PushEventAsync(sessionId, SessionEventTypes.Message, frame.Id, new
        {
            messageId = savedMessage.Id,
            parentMessageId = savedMessage.ParentMessageId,
            role = savedMessage.Role,
            agent = agentRole,
            content,
            success,
            usage,
            timing,
            model
        }, messageId: savedMessage.Id);

        return savedMessage;
    }

    /// <summary>
    /// 把编排返回的用量与时序序列化后保存为聊天消息，统一复用追加消息逻辑。
    /// Serializes orchestration usage and timing metadata before saving a chat message, reusing the shared append-message path.
    /// </summary>
    private async Task<Entities.ChatMessage> SaveMessageAsync(
        ChatFrame frame,
        string role,
        string? agentRole,
        string content,
        CancellationToken ct,
        string contentType = MessageContentTypes.Text,
        Guid? parentMessageId = null,
        Guid? messageId = null,
        OrchestrationUsage? usage = null,
        OrchestrationTiming? timing = null)
    {
        return await AppendMessageAsync(
            frame.SessionId,
            frame.Id,
            role,
            agentRole,
            content,
            contentType,
            parentMessageId,
            ct,
            messageId,
            executionPackageId: frame.ExecutionPackageId,
            originatingFrameId: frame.Id,
            tokenUsage: usage != null ? JsonSerializer.Serialize(usage) : null,
            durationMs: timing?.TotalMs);
    }

    /// <summary>
    /// 将帧标记为指定终态并记录结果时间戳；若帧已不存在则不抛错。
    /// Marks a frame with the requested terminal status and records its result timestamp; missing frames are ignored instead of throwing.
    /// </summary>
    private async Task CompleteFrameAsync(Guid frameId, string result, CancellationToken ct, string status = FrameStatus.Completed)
    {
        using var persistence = CreatePersistenceScope();
        var frame = await persistence.ChatFrames.FindAsync(frameId, ct);
        if (frame != null)
        {
            frame.Status = status;
            frame.Result = result;
            frame.CompletedAt = DateTime.UtcNow;
            await persistence.RepositoryContext.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// 为日志和事件预览截断文本，超长内容追加省略号并保留 <see langword="null"/> 语义。
    /// Truncates text for logs and event previews, appending an ellipsis for oversized content while preserving <see langword="null"/> inputs.
    /// </summary>
    private static string? Truncate(string? text, int maxLength)
    {
        if (text == null) return null;
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    /// <summary>
    /// 通过 INotificationService 发布会话事件
    /// </summary>
    private async Task PushEventAsync(
        Guid sessionId,
        string eventType,
        Guid? frameId = null,
        object? payload = null,
        Guid? messageId = null,
        Guid? executionPackageId = null,
        Guid? sourceFrameId = null,
        int? sourceEffectIndex = null)
    {
        if (!executionPackageId.HasValue && frameId.HasValue)
            executionPackageId = await GetFrameExecutionPackageIdAsync(frameId, CancellationToken.None);

        var evt = new SessionEvent
        {
            SessionId = sessionId,
            ExecutionPackageId = executionPackageId,
            FrameId = frameId,
            MessageId = messageId,
            SourceFrameId = sourceFrameId ?? frameId,
            SourceEffectIndex = sourceEffectIndex,
            EventType = eventType,
            Payload = payload != null ? JsonSerializer.Serialize(payload) : null,
            CreatedAt = DateTime.UtcNow
        };
        await _notification.PublishSessionEventAsync(sessionId, evt);
    }

    /// <summary>
    /// 发布任务状态变更事件，不直接修改任务实体；调用方需先完成实际状态流转。
    /// Publishes a task-state-changed event without mutating the task entity; callers are responsible for the actual state transition first.
    /// </summary>
    private Task PushTaskStateChangedAsync(
        Guid sessionId,
        Guid frameId,
        Guid taskId,
        string? targetRole,
        string status)
    {
        return PushTaskStateChangedCoreAsync(sessionId, frameId, taskId, targetRole, status);
    }

    private async Task PushTaskStateChangedCoreAsync(
        Guid sessionId,
        Guid frameId,
        Guid taskId,
        string? targetRole,
        string status)
    {
        var executionPackageId = await GetFrameExecutionPackageIdAsync(frameId, CancellationToken.None);
        if (executionPackageId.HasValue)
        {
            await CreateTaskExecutionLinkAsync(
                taskId,
                executionPackageId.Value,
                TaskExecutionActions.StatusChanged,
                sourceEffectIndex: null,
                CancellationToken.None);
            await UpdateExecutionPackageTaskAsync(executionPackageId.Value, taskId, CancellationToken.None);
        }

        await PushEventAsync(sessionId, SessionEventTypes.TaskStateChanged, frameId, new
        {
            taskId,
            targetRole,
            status
        }, executionPackageId: executionPackageId);
    }

    /// <summary>
    /// 获取会话中最近一条对用户可见的消息，用于把后续输入串到正确的对话线程上。
    /// Gets the latest user-visible message in a session so subsequent input can be threaded onto the correct conversation branch.
    /// </summary>
    private async Task<Guid?> GetLatestVisibleMessageIdAsync(Guid sessionId)
    {
        using var persistence = CreatePersistenceScope();
        return await persistence.ChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId && m.ContentType != MessageContentTypes.Internal)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// 优先读取内存中的当前帧，否则回退到数据库中最新的 Active 帧，兼顾热路径和进程恢复。
    /// Prefers the in-memory current frame and falls back to the newest Active frame in the database, covering both hot-path execution and process recovery.
    /// </summary>
    private async Task<Guid?> FindCurrentFrameIdAsync(Guid sessionId)
    {
        if (_currentFrame.TryGetValue(sessionId, out var currentFrameId))
            return currentFrameId;

        using var persistence = CreatePersistenceScope();
        return await persistence.ChatFrames
            .AsNoTracking()
            .Where(f => f.SessionId == sessionId && f.Status == FrameStatus.Active)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => (Guid?)f.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// 返回帧内序号最小的消息标识，也就是后续回复挂接所依赖的入口消息。
    /// Returns the earliest message id in the frame, which is the entry message later replies are threaded against.
    /// </summary>
    private async Task<Guid?> GetFrameEntryMessageIdAsync(Guid frameId, CancellationToken ct)
    {
        using var persistence = CreatePersistenceScope();
        return await persistence.ChatMessages
            .AsNoTracking()
            .Where(m => m.FrameId == frameId)
            .OrderBy(m => m.SequenceNo)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// 加载帧的首条消息全文，用于恢复运行时输入角色、内容和父消息链路。
    /// Loads the frame's first message so runtime input role, content, and parent-message linkage can be reconstructed.
    /// </summary>
    private async Task<Entities.ChatMessage?> GetFrameEntryMessageAsync(Guid frameId, CancellationToken ct)
    {
        using var persistence = CreatePersistenceScope();
        return await persistence.ChatMessages
            .AsNoTracking()
            .Where(message => message.FrameId == frameId)
            .OrderBy(message => message.SequenceNo)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<ChatFrame?> GetFrameAsync(Guid frameId, CancellationToken ct)
    {
        using var persistence = CreatePersistenceScope();
        return await persistence.ChatFrames
            .AsNoTracking()
            .FirstOrDefaultAsync(frame => frame.Id == frameId, ct);
    }

    /// <summary>
    /// 根据帧关联任务解析已分配的项目智能体；没有任务时直接返回 <see langword="null"/>。
    /// Resolves the assigned project agent from the frame's task; returns <see langword="null"/> immediately when the frame is not task-backed.
    /// </summary>
    private async Task<Guid?> ResolveFrameProjectAgentIdAsync(Guid? taskId, CancellationToken ct)
    {
        if (!taskId.HasValue)
            return null;

        using var persistence = CreatePersistenceScope();
        return await persistence.Tasks
            .AsNoTracking()
            .Where(task => task.Id == taskId.Value)
            .Select(task => task.AssignedProjectAgentRoleId)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<bool> IsSecretaryTargetAsync(ChatFrame frame, CancellationToken ct)
    {
        using var persistence = CreatePersistenceScope();

        if (frame.TargetProjectAgentRoleId.HasValue)
        {
            var projectAgentRole = await persistence.ProjectAgentRoles
                .AsNoTracking()
                .Include(item => item.AgentRole)
                .FirstOrDefaultAsync(item => item.Id == frame.TargetProjectAgentRoleId.Value, ct);

            return projectAgentRole?.AgentRole?.IsBuiltin == true;
        }

        if (frame.TargetAgentRoleId.HasValue)
        {
            var role = await persistence.AgentRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == frame.TargetAgentRoleId.Value, ct);

            return role?.IsBuiltin == true;
        }

        return false;
    }

    private async Task<string> ResolveFrameTargetDisplayAsync(ChatFrame frame, CancellationToken ct)
    {
        return await ResolveRoleDisplayAsync(frame.TargetProjectAgentRoleId, frame.TargetAgentRoleId, ct)
            ?? BuiltinRoleTypes.Secretary;
    }

    private async Task<string?> ResolveFrameInitiatorDisplayAsync(ChatFrame frame, CancellationToken ct)
    {
        return await ResolveRoleDisplayAsync(frame.InitiatorProjectAgentRoleId, frame.InitiatorAgentRoleId, ct);
    }

    private async Task<string?> ResolveRoleDisplayAsync(Guid? projectAgentRoleId, Guid? agentRoleId, CancellationToken ct)
    {
        using var persistence = CreatePersistenceScope();

        if (projectAgentRoleId.HasValue)
        {
            var projectAgentRole = await persistence.ProjectAgentRoles
                .AsNoTracking()
                .Include(item => item.AgentRole)
                .FirstOrDefaultAsync(item => item.Id == projectAgentRoleId.Value, ct);

            if (projectAgentRole?.AgentRole != null)
            {
                return !string.IsNullOrWhiteSpace(projectAgentRole.AgentRole.JobTitle)
                    ? projectAgentRole.AgentRole.JobTitle
                    : projectAgentRole.AgentRole.Name;
            }
        }

        if (agentRoleId.HasValue)
        {
            var role = await persistence.AgentRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == agentRoleId.Value, ct);

            if (role != null)
            {
                return !string.IsNullOrWhiteSpace(role.JobTitle)
                    ? AgentJobTitleCatalog.NormalizeKey(role.JobTitle) ?? role.JobTitle
                    : role.Name;
            }
        }

        return null;
    }

    private async Task<(Guid? AgentRoleId, Guid? ProjectAgentRoleId)> ResolveRoleIdentityAsync(
        PersistenceScope persistence,
        Guid? projectId,
        string? roleDisplay,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(roleDisplay))
        {
            return (null, null);
        }

        var normalizedRole = roleDisplay.Trim();

        if (projectId.HasValue)
        {
            var projectRoles = await persistence.ProjectAgentRoles
                .AsNoTracking()
                .Include(item => item.AgentRole)
                .Where(item => item.ProjectId == projectId.Value && item.AgentRole != null && item.AgentRole.IsActive)
                .OrderByDescending(item => item.AgentRole!.IsBuiltin)
                .ThenBy(item => item.CreatedAt)
                .ToListAsync(ct);

            var projectAgentRole = projectRoles
                .FirstOrDefault(item => MatchesRoleDisplay(item.AgentRole!, normalizedRole));

            if (projectAgentRole != null)
            {
                return (projectAgentRole.AgentRoleId, projectAgentRole.Id);
            }
        }

        var globalRoles = await persistence.AgentRoles
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.IsBuiltin)
            .ThenBy(item => item.CreatedAt)
            .ToListAsync(ct);

        var agentRole = globalRoles.FirstOrDefault(item => MatchesRoleDisplay(item, normalizedRole));

        return agentRole == null ? (null, null) : (agentRole.Id, null);
    }

    private static async Task<(Guid? AgentRoleId, Guid? ProjectAgentRoleId)> ResolveProjectRoleIdentityAsync(
        PersistenceScope persistence,
        Guid projectAgentRoleId,
        CancellationToken ct)
    {
        var projectAgentRole = await persistence.ProjectAgentRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectAgentRoleId, ct);

        return projectAgentRole == null
            ? (null, null)
            : (projectAgentRole.AgentRoleId, projectAgentRole.Id);
    }

    private static bool MatchesRoleDisplay(AgentRole role, string targetRole)
    {
        return string.Equals(role.Name, targetRole, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                AgentJobTitleCatalog.NormalizeKey(role.JobTitle),
                AgentJobTitleCatalog.NormalizeKey(targetRole),
                StringComparison.OrdinalIgnoreCase)
            || (role.IsBuiltin && AgentJobTitleCatalog.IsSecretary(targetRole));
    }

    /// <summary>
    /// zh-CN: 读取帧所属执行包标识。
    /// 这里单独抽方法，是为了让事件/消息/任务索引在不知道完整帧对象的情况下也能稳定回指执行包。
    /// en: Resolves the execution-package id that owns a frame.
    /// </summary>
    private async Task<Guid?> GetFrameExecutionPackageIdAsync(Guid? frameId, CancellationToken ct)
    {
        if (!frameId.HasValue)
            return null;

        using var persistence = CreatePersistenceScope();
        return await persistence.ChatFrames
            .AsNoTracking()
            .Where(frame => frame.Id == frameId.Value)
            .Select(frame => frame.ExecutionPackageId)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// zh-CN: 读取执行包的入口类型，供运行时上下文和监控投影复用。
    /// en: Resolves the persisted entry kind for an execution package.
    /// </summary>
    private async Task<string?> GetExecutionPackageEntryKindAsync(Guid? executionPackageId, CancellationToken ct)
    {
        if (!executionPackageId.HasValue)
            return null;

        using var persistence = CreatePersistenceScope();
        return await persistence.ExecutionPackages
            .AsNoTracking()
            .Where(package => package.Id == executionPackageId.Value)
            .Select(package => package.EntryKind)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// zh-CN: 创建一条新的入口执行包。
    /// 当前先把 session 级输入稳定落成“一个输入对应一个包”，后面再继续扩展子包/重试包。
    /// 例如：
    /// - 脑暴中用户发一条新消息 -> 新建一个 project_brainstorm package
    /// - 群聊中用户发一条 @成员 指令 -> 新建一个 project_group package
    /// en: Creates a new entry execution package for one session-level user input.
    /// </summary>
    private async Task<Guid> CreateExecutionPackageAsync(
        ChatSession session,
        string input,
        string entryKind,
        string? initiatorRole,
        string? targetRole,
        CancellationToken ct)
    {
        using var persistence = CreatePersistenceScope();
        var initiatorIdentity = await ResolveRoleIdentityAsync(persistence, session.ProjectId, initiatorRole, ct);
        var targetIdentity = await ResolveRoleIdentityAsync(persistence, session.ProjectId, targetRole, ct);
        var package = new ExecutionPackage
        {
            ProjectId = session.ProjectId,
            SessionId = session.Id,
            EntryKind = entryKind,
            PackageKind = ExecutionPackageKinds.Entry,
            Scene = session.Scene,
            Status = ExecutionPackageStatus.Active,
            InputSummary = Truncate(input, 4000),
            InitiatorAgentRoleId = initiatorIdentity.AgentRoleId,
            InitiatorProjectAgentRoleId = initiatorIdentity.ProjectAgentRoleId,
            TargetAgentRoleId = targetIdentity.AgentRoleId,
            TargetProjectAgentRoleId = targetIdentity.ProjectAgentRoleId
        };

        persistence.ExecutionPackages.Add(package);
        await persistence.RepositoryContext.SaveChangesAsync(ct);
        return package.Id;
    }

    /// <summary>
    /// zh-CN: 更新执行包状态。
    /// 当前实现先只维护数据库里的“最近可见状态”，为浏览器调试和后续清历史脚本提供稳定锚点。
    /// en: Updates the latest visible execution-package status.
    /// </summary>
    private async Task UpdateExecutionPackageStatusAsync(Guid executionPackageId, string status, bool complete, CancellationToken ct)
    {
        using var persistence = CreatePersistenceScope();
        var package = await persistence.ExecutionPackages.FindAsync(new object[] { executionPackageId }, ct);
        if (package == null)
            return;

        package.Status = status;
        package.UpdatedAt = DateTime.UtcNow;
        package.CompletedAt = complete ? DateTime.UtcNow : null;
        await persistence.RepositoryContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// zh-CN: 把任务锚点补回执行包。
    /// 项目群聊里任务通常是在秘书分派后才生成真实 taskId，所以这里做一次回填，而不是要求创建包时就提前知道。
    /// en: Backfills the task anchor onto an execution package once the real task id becomes available.
    /// </summary>
    private async Task UpdateExecutionPackageTaskAsync(Guid executionPackageId, Guid taskId, CancellationToken ct)
    {
        using var persistence = CreatePersistenceScope();
        var package = await persistence.ExecutionPackages.FindAsync(new object[] { executionPackageId }, ct);
        if (package == null)
            return;

        package.TaskId ??= taskId;
        package.UpdatedAt = DateTime.UtcNow;
        await persistence.RepositoryContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// zh-CN: 创建任务与执行包之间的查询索引。
    /// 这不是主事实源，主事实仍然保留在执行包和任务元数据里；这里的目标只是让 UI 能快速从任务跳回对应执行包。
    /// en: Creates a query-oriented projection link between a task and an execution package.
    /// </summary>
    private async Task CreateTaskExecutionLinkAsync(
        Guid taskId,
        Guid executionPackageId,
        string action,
        int? sourceEffectIndex,
        CancellationToken ct)
    {
        using var persistence = CreatePersistenceScope();
        persistence.TaskExecutionLinks.Add(new TaskExecutionLink
        {
            TaskId = taskId,
            ExecutionPackageId = executionPackageId,
            Action = action,
            SourceEffectIndex = sourceEffectIndex
        });
        await persistence.RepositoryContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// zh-CN: 按会话场景推导稳定的入口类型。
    /// 脑暴之所以映射成 project_brainstorm，而不是 generic session_reply，是为了保留“项目内秘书私聊”这个业务语义。
    /// en: Resolves the stable entry kind persisted with execution packages from the current session scene.
    /// </summary>
    private static string ResolveEntryKind(string scene) => scene switch
    {
        SessionSceneTypes.ProjectGroup => ExecutionEntryKinds.ProjectGroup,
        SessionSceneTypes.Private => ExecutionEntryKinds.ProjectAgentPrivate,
        SessionSceneTypes.Test => ExecutionEntryKinds.TestChat,
        _ => ExecutionEntryKinds.ProjectBrainstorm
    };

    /// <summary>
    /// zh-CN: 把 Runner 内部的一轮执行结果整形成统一对话任务输出。
    /// 这里统一落在一个方法里，是为了避免创建会话、续写会话两条链各自拼字段后再次漂移。
    /// en: Shapes an internal runner turn result into the unified conversation-task output.
    /// </summary>
    private static ConversationTaskOutput CreateConversationTaskOutput(
        ChatSession session,
        Guid taskId,
        string status,
        string entryKind,
        bool isAwaitingInput)
    {
        return new ConversationTaskOutput
        {
            TaskId = taskId,
            Status = status,
            SessionId = session.Id,
            ProjectId = session.ProjectId,
            Scene = session.Scene,
            EntryKind = entryKind,
            IsAwaitingInput = isAwaitingInput
        };
    }

    /// <summary>
    /// zh-CN: 按会话场景推导默认入口目标角色。
    /// 这里把脑暴固定成秘书，是为了把“脑暴=项目内秘书私聊”的模型显式落到事实层，而不是只停在讨论里。
    /// en: Resolves the default target role for a scene.
    /// </summary>
    private static string? ResolveDefaultTargetRole(string scene) => scene switch
    {
        SessionSceneTypes.ProjectBrainstorm => BuiltinRoleTypes.Secretary,
        SessionSceneTypes.ProjectGroup => BuiltinRoleTypes.Secretary,
        _ => null
    };

    /// <summary>
    /// 把持久化消息角色转换为 AI 运行时角色；未知值按用户消息处理以维持兼容性。
    /// Maps a persisted message role to the AI runtime role; unknown values are treated as user messages for compatibility.
    /// </summary>
    private static ChatRole ToChatRole(string role)
    {
        return role switch
        {
            MessageRoles.Assistant => ChatRole.Assistant,
            MessageRoles.System => ChatRole.System,
            MessageRoles.Tool => ChatRole.Tool,
            _ => ChatRole.User
        };
    }

    /// <summary>
    /// 将会话场景枚举映射为消息服务场景；未识别场景默认回到 <c>ProjectBrainstorm</c>。
    /// Maps the session scene enum to the message-service scene and defaults unrecognized values to <c>ProjectBrainstorm</c>.
    /// </summary>
    private static MessageScene ToMessageScene(SceneType? scene)
    {
        return scene switch
        {
            SceneType.Test => MessageScene.Test,
            SceneType.Private => MessageScene.Private,
            SceneType.TeamGroup => MessageScene.TeamGroup,
            SceneType.ProjectGroup => MessageScene.ProjectGroup,
            _ => MessageScene.ProjectBrainstorm
        };
    }

    /// <summary>
    /// 以帧内最大序号为基准追加一条消息并持久化；序号在数据库中递增，保证单帧消息顺序稳定。
    /// Appends and persists a message using the frame's current maximum sequence number so ordering remains stable within a frame.
    /// </summary>
    private async Task<Entities.ChatMessage> AppendMessageAsync(
        Guid sessionId,
        Guid frameId,
        string role,
        string? agentRole,
        string content,
        string contentType,
        Guid? parentMessageId,
        CancellationToken ct,
        Guid? messageId = null,
        Guid? executionPackageId = null,
        Guid? originatingFrameId = null,
        string? tokenUsage = null,
        long? durationMs = null)
    {
        using var persistence = CreatePersistenceScope();
        var sessionProjectId = await persistence.ChatSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId)
            .Select(item => item.ProjectId)
            .FirstOrDefaultAsync(ct);
        var messageIdentity = await ResolveRoleIdentityAsync(persistence, sessionProjectId, agentRole, ct);

        var maxSeq = await persistence.ChatMessages
            .Where(m => m.FrameId == frameId)
            .MaxAsync(m => (int?)m.SequenceNo, ct) ?? 0;

        var message = new Entities.ChatMessage
        {
            Id = messageId ?? Guid.NewGuid(),
            FrameId = frameId,
            SessionId = sessionId,
            ExecutionPackageId = executionPackageId,
            OriginatingFrameId = originatingFrameId ?? frameId,
            ParentMessageId = parentMessageId,
            Role = role,
            AgentRoleId = messageIdentity.AgentRoleId,
            ProjectAgentRoleId = messageIdentity.ProjectAgentRoleId,
            Content = content,
            ContentType = contentType,
            SequenceNo = maxSeq + 1,
            TokenUsage = tokenUsage,
            DurationMs = durationMs
        };

        persistence.ChatMessages.Add(message);
        await persistence.RepositoryContext.SaveChangesAsync(ct);
        return message;
    }

    /// <summary>
    /// 根据系统审批策略生成能力申请处理方案；命中白名单时会尝试立即刷新能力，因此可能带来可重试的前置副作用。
    /// Builds the handling plan for a capability request based on system approval policy; allowlisted requests may trigger an immediate capability refresh, which is a deliberate prerequisite side effect for retry.
    /// </summary>
    private async Task<ProjectGroupCapabilityPlan> BuildCapabilityPlanAsync(
        Guid projectId,
        Guid frameId,
        string? roleType,
        ProjectGroupCapabilityRequest request,
        CancellationToken ct)
    {
        var tools = request.RequiredTools.Count > 0
            ? string.Join(", ", request.RequiredTools)
            : "未指明能力";
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? string.Empty
            : $"，原因：{request.Reason}";

        using var scope = _scopeFactory.CreateScope();
        var settingsApiService = scope.ServiceProvider.GetRequiredService<ISettingsApiService>();
        var settings = await settingsApiService.GetSystemAsync(ct);

        var autoApproveCapabilityRequest = settings.AutoApproveProjectGroupCapabilities;

        if (autoApproveCapabilityRequest)
        {
            // zh-CN: 自动审批开启后，直接尝试刷新能力并原地重试，尽量不把可自动处理的申请再抛回给用户。
            // en: When auto approval is enabled, try to refresh capabilities and retry in place before escalating the request back to the user.
            var grantResult = await _projectGroupCapability.TryPrepareCapabilityRetryAsync(projectId, frameId, request, ct);
            if (grantResult.CanRetryWithoutPenalty)
            {
                return new ProjectGroupCapabilityPlan(
                    request,
                    $"{roleType ?? "该智能体"} 申请额外能力：{tools}{reason}。系统当前已开启自动审批，秘书已刷新该智能体的可用能力，正在继续执行。",
                    RetryWithoutPenalty: true);
            }

            var detail = string.IsNullOrWhiteSpace(grantResult.Detail)
                ? "请补齐对应工具配置后继续任务。"
                : grantResult.Detail;
            return new ProjectGroupCapabilityPlan(
                request,
                $"{roleType ?? "该智能体"} 申请额外能力：{tools}{reason}。系统当前已开启自动审批，但{detail}",
                RetryWithoutPenalty: false);
        }

        return new ProjectGroupCapabilityPlan(
            request,
            $"{roleType ?? "该智能体"} 申请额外能力：{tools}{reason}。当前策略要求先征求用户确认，请在群里决定是否允许。",
            RetryWithoutPenalty: false);
    }

    private PersistenceScope CreatePersistenceScope()
    {
        var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        return new PersistenceScope(
            scope,
            services.GetRequiredService<IRepositoryContext>(),
            services.GetRequiredService<IChatSessionRepository>(),
            services.GetRequiredService<IChatFrameRepository>(),
            services.GetRequiredService<IChatMessageRepository>(),
            services.GetRequiredService<ITaskItemRepository>(),
            services.GetRequiredService<IExecutionPackageRepository>(),
            services.GetRequiredService<ITaskExecutionLinkRepository>(),
            services.GetRequiredService<IProjectAgentRoleRepository>(),
            services.GetRequiredService<IAgentRoleRepository>());
    }

    private sealed record FrameExecutionResult(
        string Content,
        bool Success,
        bool ShouldRetry = true,
        string? SecretaryNotice = null,
        bool RetryWithoutPenalty = false,
        bool RequiresUserInput = false);
    private sealed record ProjectGroupDispatchPlan(string DisplayContent, IReadOnlyList<ProjectGroupDispatchTarget> DispatchTargets);
    private sealed record ProjectGroupOrchestratorExecutionResult(string VisibleReply, ProjectGroupDispatchPlan? DispatchPlan);
    private sealed record ProjectGroupOrchestratorRelayPlan(string Purpose);
    private sealed record ProjectGroupCapabilityPlan(
        ProjectGroupCapabilityRequest Request,
        string SecretaryNotice,
        bool RetryWithoutPenalty);
    private sealed class PersistenceScope : IDisposable
    {
        private readonly IServiceScope _scope;

        public PersistenceScope(
            IServiceScope scope,
            IRepositoryContext repositoryContext,
            IChatSessionRepository chatSessions,
            IChatFrameRepository chatFrames,
            IChatMessageRepository chatMessages,
            ITaskItemRepository tasks,
            IExecutionPackageRepository executionPackages,
            ITaskExecutionLinkRepository taskExecutionLinks,
            IProjectAgentRoleRepository projectAgentRoles,
            IAgentRoleRepository agentRoles)
        {
            _scope = scope;
            RepositoryContext = repositoryContext;
            ChatSessions = chatSessions;
            ChatFrames = chatFrames;
            ChatMessages = chatMessages;
            Tasks = tasks;
            ExecutionPackages = executionPackages;
            TaskExecutionLinks = taskExecutionLinks;
            ProjectAgentRoles = projectAgentRoles;
            AgentRoles = agentRoles;
        }

        public IRepositoryContext RepositoryContext { get; }

        public IChatSessionRepository ChatSessions { get; }

        public IChatFrameRepository ChatFrames { get; }

        public IChatMessageRepository ChatMessages { get; }

        public ITaskItemRepository Tasks { get; }

        public IExecutionPackageRepository ExecutionPackages { get; }

        public ITaskExecutionLinkRepository TaskExecutionLinks { get; }

        public IProjectAgentRoleRepository ProjectAgentRoles { get; }

        public IAgentRoleRepository AgentRoles { get; }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }

    private readonly record struct SessionTaskHandle(
        ChatSession Session,
        Guid TaskId,
        string Status,
        string EntryKind,
        bool IsAwaitingInput);
}



