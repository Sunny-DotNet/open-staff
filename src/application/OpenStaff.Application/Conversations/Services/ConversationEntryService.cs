using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent.Services;
using OpenStaff.Application.AgentRoles.Services;
using OpenStaff.Application.Conversations.Models;
using OpenStaff.Application.Sessions.Services;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Orchestration;
using OpenStaff.Core.Notifications;
using OpenStaff.Dtos;
using OpenStaff.Entities;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Conversations.Services;

/// <summary>
/// zh-CN: 统一对话入口服务。
/// 这一层的职责不是替代底层运行时，而是先把“不同 API 的业务入口语义”收口到一个地方，
/// 再由它决定继续走哪个底层执行器。
/// 这样做的原因是：
/// 1. 先把入口统一，避免每个 API 自己拼 CreateMessageRequest；
/// 2. 再逐步把更深层的 session / execution package / task 投影继续往这里收；
/// 3. 旧的 SessionRunner 和 AgentService 先退成底层执行器，避免首轮改造面失控。
/// 例如：
/// - 测试对话会在这里统一转成 TestChat 入口，而不是 AgentRoleApiService 自己组装运行时请求；
/// - 项目私聊也会在这里统一校验并转成 Private 入口，而不是 ProjectAgentService 自己硬编码。
/// en: Unified application-layer conversation entry service.
/// </summary>
public sealed class ConversationEntryService
{
    private readonly IAgentRoleRepository _agentRoles;
    private readonly IProjectAgentRoleRepository _projectAgents;
    private readonly IAgentService _agentService;
    private readonly SessionStreamManager _streamManager;
    private readonly TaskStreamManager _taskStreamManager;
    private readonly ILogger<ConversationEntryService> _logger;
    private readonly SessionRunner? _sessionRunner;

    /// <summary>
    /// zh-CN: 初始化统一入口服务。
    /// SessionRunner 保持可选，是为了让现有单测可以只验证测试对话/项目私聊，不必强行搭整套会话执行环境。
    /// en: Initializes the unified entry service.
    /// </summary>
    public ConversationEntryService(
        IAgentRoleRepository agentRoles,
        IProjectAgentRoleRepository projectAgents,
        IAgentService agentService,
        SessionStreamManager streamManager,
        TaskStreamManager taskStreamManager,
        ILogger<ConversationEntryService> logger,
        SessionRunner? sessionRunner = null)
    {
        _agentRoles = agentRoles;
        _projectAgents = projectAgents;
        _agentService = agentService;
        _streamManager = streamManager;
        _taskStreamManager = taskStreamManager;
        _logger = logger;
        _sessionRunner = sessionRunner;
    }

    /// <summary>
    /// zh-CN: 进入角色测试对话入口。
    /// 这里继续沿用现有的瞬态流模式，但由统一入口服务负责把“业务入口”翻译成底层运行时请求。
    /// en: Starts a role test-chat entry flow.
    /// </summary>
    public async Task<ConversationTaskOutput> StartTestChatAsync(TestChatEntry entry, CancellationToken ct)
    {
        ValidateTextInput(entry.Input, nameof(entry));

        var sourceRole = await _agentRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == entry.AgentRoleId && r.IsActive, ct)
            ?? throw new KeyNotFoundException("Agent role not found");

        // zh-CN: 这里仍然保留一份“展示用”的角色快照，原因是测试对话前端需要在最终事件里看到本次实际采用的角色画像；
        // 真正执行时，运行时仍会按 AgentRoleId + OverrideJson 再次解析，避免入口层和运行时各自持有不同事实源。
        // 例如：用户临时把 architect 的 modelName 改成 gpt-4.1，这个快照会用于最终测试结果展示，但不会直接替代数据库角色实体进入运行时。
        // en: Keep a display-oriented effective-role snapshot for test-chat result shaping while the runtime still resolves the true execution profile from AgentRoleId + OverrideJson.
        var effectiveRole = AgentRoleExecutionProfileFactory.CreateEffectiveRole(sourceRole, entry.Override);

        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        _streamManager.Create(sessionId);
        PushTransientTaskEvent(
            taskId,
            CreateTaskEvent(
                sessionId,
                taskId,
                SessionEventTypes.UserInput,
                payload: new { content = entry.Input, input = entry.Input }));

        var runtimeRequest = new CreateMessageRequest(
            Scene: MessageScene.Test,
            MessageContext: new MessageContext(
                ProjectId: null,
                SessionId: sessionId,
                ParentMessageId: null,
                FrameId: null,
                ParentFrameId: null,
                TaskId: null,
                ProjectAgentRoleId: null,
                TargetRole: null,
                InitiatorRole: null,
                Extra: null)
            {
                ExecutionPackageId = taskId,
                EntryKind = ExecutionEntryKinds.TestChat
            },
            InputRole: ChatRole.User,
            Input: entry.Input,
            AgentRoleId: entry.AgentRoleId,
            OverrideJson: entry.Override == null ? null : JsonSerializer.Serialize(entry.Override));

        var response = await _agentService.CreateMessageAsync(runtimeRequest, ct);
        if (!_agentService.TryGetMessageHandler(response.MessageId, out var handler) || handler == null)
            throw new InvalidOperationException($"Message handler '{response.MessageId}' was not created.");

        _ = Task.Run(
            () => CompleteTransientConversationTaskAsync(
                sessionId,
                taskId,
                handler,
                AgentJobTitleCatalog.NormalizeKey(effectiveRole.JobTitle) ?? effectiveRole.Name),
            CancellationToken.None);

        return new ConversationTaskOutput
        {
            TaskId = taskId,
            Status = ExecutionPackageStatus.Active,
            SessionId = sessionId,
            Scene = SessionSceneTypes.Test,
            EntryKind = ExecutionEntryKinds.TestChat,
            AgentRoleId = entry.AgentRoleId,
            IsAwaitingInput = false
        };
    }

    /// <summary>
    /// zh-CN: 进入项目头脑风暴入口。
    /// 当前先复用 SessionRunner 做正式会话执行，但入口语义已经集中到这里，后续再把 session/package 的创建继续前移时，
    /// 上层 API 不需要再改第二遍。
    /// en: Starts a project-brainstorm session through the unified entry service.
    /// </summary>
    public Task<ConversationTaskOutput> StartProjectBrainstormAsync(ProjectBrainstormEntry entry, CancellationToken ct)
    {
        ValidateTextInput(entry.Input, nameof(entry));
        return RequireSessionRunner().StartSessionTaskAsync(
            entry.ProjectId,
            entry.Input,
            entry.ContextStrategy,
            SessionSceneTypes.ProjectBrainstorm);
    }

    /// <summary>
    /// zh-CN: 进入项目群聊入口。
    /// 这里仍然固定由项目群聊场景接管后续编排，mentions 暂时作为强类型入口保留下来，供后续入口编排前移时直接复用。
    /// en: Starts a project-group session through the unified entry service.
    /// </summary>
    public Task<ConversationTaskOutput> StartProjectGroupAsync(ProjectGroupEntry entry, CancellationToken ct)
    {
        ValidateTextInput(entry.Input, nameof(entry));
        return RequireSessionRunner().StartSessionTaskAsync(
            entry.ProjectId,
            entry.Input,
            entry.ContextStrategy,
            SessionSceneTypes.ProjectGroup,
            entry.Mentions,
            entry.DisplayInput);
    }

    /// <summary>
    /// zh-CN: 向现有正式会话续写一条消息。
    /// 这里故意只保留 sessionId + input，不让调用方再次传 scene / target / frame 元数据，
    /// 因为这些事实已经固化在会话内部，重复让上层传只会制造更多不一致。
    /// en: Sends a follow-up message into an existing persistent session.
    /// </summary>
    public Task<ConversationTaskOutput> SendSessionReplyAsync(SessionReplyEntry entry, CancellationToken ct)
    {
        ValidateTextInput(entry.Input, nameof(entry));
        return RequireSessionRunner().SendMessageTaskAsync(entry.SessionId, entry.Input, entry.Mentions, entry.DisplayInput);
    }

    /// <summary>
    /// zh-CN: 进入项目成员私聊入口。
    /// 和测试对话不同，这里不是“试跑角色”，而是明确对某个项目成员发消息，所以 projectId + projectAgentId 都必须同时成立。
    /// en: Sends a direct message to a project agent through the unified entry service.
    /// </summary>
    public async Task<ConversationTaskOutput> SendProjectAgentPrivateAsync(ProjectAgentPrivateEntry entry, CancellationToken ct)
    {
        ValidateTextInput(entry.Input, nameof(entry));

        var projectAgent = await _projectAgents
            .AsNoTracking()
            .Include(item => item.AgentRole)
            .FirstOrDefaultAsync(item => item.Id == entry.ProjectAgentRoleId && item.ProjectId == entry.ProjectId, ct)
            ?? throw new KeyNotFoundException($"Project agent '{entry.ProjectAgentRoleId}' was not found in project '{entry.ProjectId}'.");

        if (projectAgent.AgentRole == null)
            throw new InvalidOperationException($"Project agent '{entry.ProjectAgentRoleId}' does not have a role.");

        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        _streamManager.Create(sessionId);
        PushTransientTaskEvent(
            taskId,
            CreateTaskEvent(
                sessionId,
                taskId,
                SessionEventTypes.UserInput,
                payload: new { content = entry.Input, input = entry.Input }));

        var runtimeResponse = await _agentService.CreateMessageAsync(
            new CreateMessageRequest(
                Scene: MessageScene.Private,
                MessageContext: new MessageContext(
                    ProjectId: entry.ProjectId,
                    SessionId: sessionId,
                    ParentMessageId: null,
                    FrameId: null,
                    ParentFrameId: null,
                    TaskId: null,
                    ProjectAgentRoleId: entry.ProjectAgentRoleId,
                    TargetRole: AgentJobTitleCatalog.NormalizeKey(projectAgent.AgentRole.JobTitle) ?? projectAgent.AgentRole.Name,
                    InitiatorRole: MessageRoles.User,
                    Extra: null)
                {
                    ExecutionPackageId = taskId,
                    EntryKind = ExecutionEntryKinds.ProjectAgentPrivate
                },
                InputRole: ChatRole.User,
                Input: entry.Input),
            ct);

        if (!_agentService.TryGetMessageHandler(runtimeResponse.MessageId, out var handler) || handler == null)
            throw new InvalidOperationException($"Message handler '{runtimeResponse.MessageId}' was not created.");

        _ = Task.Run(
            () => CompleteTransientConversationTaskAsync(
                sessionId,
                taskId,
                handler,
                AgentJobTitleCatalog.NormalizeKey(projectAgent.AgentRole?.JobTitle) ?? projectAgent.AgentRole?.Name ?? "secretary"),
            CancellationToken.None);

        return new ConversationTaskOutput
        {
            TaskId = taskId,
            Status = ExecutionPackageStatus.Active,
            SessionId = sessionId,
            ProjectId = entry.ProjectId,
            Scene = SessionSceneTypes.Private,
            EntryKind = ExecutionEntryKinds.ProjectAgentPrivate,
            ProjectAgentRoleId = entry.ProjectAgentRoleId,
            AgentRoleId = projectAgent.AgentRoleId,
            IsAwaitingInput = false
        };
    }

    /// <summary>
    /// zh-CN: 完成测试聊天流并收尾瞬态处理器。
    /// 单独抽到这里，是为了让“测试对话入口”也和其他入口一样通过统一入口服务闭环，而不是继续把收尾逻辑散落在 API Service 里。
    /// en: Completes a transient test-chat stream and cleans up the runtime handler.
    /// </summary>
    private async Task CompleteTransientConversationTaskAsync(
        Guid sessionId,
        Guid taskId,
        MessageHandler handler,
        string runtimeRole)
    {
        try
        {
            var summary = await handler.Completion;
            var resolvedRole = !string.IsNullOrWhiteSpace(summary.AgentRole) ? summary.AgentRole : runtimeRole;

            if (summary.Success)
            {
                PushTransientTaskEvent(
                    taskId,
                    CreateTaskEvent(
                        sessionId,
                        taskId,
                        SessionEventTypes.Message,
                        messageId: summary.MessageId,
                        payload: new
                        {
                            messageId = summary.MessageId,
                            parentMessageId = (Guid?)null,
                            role = MessageRoles.Assistant,
                            agent = resolvedRole,
                            content = summary.Content,
                            success = true,
                            usage = summary.Usage,
                            timing = summary.Timing,
                            model = summary.Model
                        }));
            }
            else
            {
                PushTransientTaskEvent(
                    taskId,
                    CreateTaskEvent(
                        sessionId,
                        taskId,
                        SessionEventTypes.Error,
                        messageId: summary.MessageId,
                        payload: new
                        {
                            error = summary.Error ?? "Message execution failed.",
                            message = summary.Error ?? "Message execution failed.",
                            role = resolvedRole,
                            model = summary.Model
                        }));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversation task {TaskId} completion failed.", taskId);
            PushTransientTaskEvent(
                taskId,
                CreateTaskEvent(
                    sessionId,
                    taskId,
                    SessionEventTypes.Error,
                    payload: new
                    {
                        error = ex.Message,
                        message = ex.Message,
                        role = runtimeRole
                    }));
        }
        finally
        {
            _agentService.RemoveMessageHandler(handler.MessageId);
            _streamManager.CompleteTransient(sessionId);
            _taskStreamManager.CompleteTransient(taskId);
        }
    }

    private void PushTransientTaskEvent(Guid taskId, SessionEvent evt)
    {
        _taskStreamManager.Push(taskId, evt);
    }

    private static SessionEvent CreateTaskEvent(
        Guid sessionId,
        Guid taskId,
        string eventType,
        object payload,
        Guid? messageId = null)
    {
        return new SessionEvent
        {
            SessionId = sessionId,
            ExecutionPackageId = taskId,
            MessageId = messageId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// zh-CN: 当前阶段仍要求文本输入非空。
    /// 后面真正把 attachments/material pipeline 接上时，再放宽成“文本或附件至少有一个”。
    /// 先保持严格，是为了不在统一入口刚接管时顺带引入新的空输入歧义。
    /// en: Validates the current text-first entry contract.
    /// </summary>
    private static void ValidateTextInput(string? input, string paramName)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input is required", paramName);
    }

    private SessionRunner RequireSessionRunner()
        => _sessionRunner ?? throw new InvalidOperationException("SessionRunner is required for persistent conversation entries.");
}
