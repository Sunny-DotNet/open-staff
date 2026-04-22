using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent.Services;
using OpenStaff.Application.Conversations.Models;
using OpenStaff.Application.Conversations.Services;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.Core.Orchestration;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Agents.Services;
/// <summary>
/// 项目智能体辅助服务，负责查询项目成员和执行私聊消息。
/// Project-agent helper service that queries project members and executes direct messages.
/// </summary>
public class ProjectAgentService
{
    private readonly IProjectAgentRoleRepository _projectAgents;
    private readonly IAgentEventRepository _agentEvents;
    private readonly IAgentService _agentService;
    private readonly ILogger<ProjectAgentService> _logger;
    private readonly ConversationEntryService? _conversationEntryService;

    /// <summary>
    /// 初始化项目智能体辅助服务。
    /// Initializes the project-agent helper service.
    /// </summary>
    public ProjectAgentService(
        IProjectAgentRoleRepository projectAgents,
        IAgentEventRepository agentEvents,
        IAgentService agentService,
        ILogger<ProjectAgentService> logger,
        ConversationEntryService? conversationEntryService = null)
    {
        _projectAgents = projectAgents;
        _agentEvents = agentEvents;
        _agentService = agentService;
        _logger = logger;
        _conversationEntryService = conversationEntryService;
    }

    /// <summary>
    /// 获取项目参与的智能体列表。
    /// Gets the agents participating in a project.
    /// </summary>
    public async Task<List<ProjectAgentRole>> GetProjectAgentsAsync(Guid projectId, CancellationToken ct)
    {
        return await _projectAgents
            .Where(a => a.ProjectId == projectId)
            .Include(a => a.AgentRole)
            .ToListAsync(ct);
    }

    /// <summary>
    /// 分页获取智能体事件。
    /// Gets a paged slice of events for a project agent.
    /// </summary>
    public async Task<List<AgentEvent>> GetAgentEventsAsync(Guid projectId, Guid projectAgentRoleId, int page, int pageSize, CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20; // Limit page size

            _logger.LogDebug("Fetching events for project agent role {ProjectAgentRoleId} in project {ProjectId}, page {Page}", projectAgentRoleId, projectId, page);

            var events = await _agentEvents
                .Where(e => e.ProjectId == projectId && e.ProjectAgentRoleId == projectAgentRoleId)
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("Retrieved {Count} events for project agent role {ProjectAgentRoleId} in {ElapsedMs}ms", events.Count, projectAgentRoleId, elapsed);

            return events;
        }
        catch (Exception ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Error fetching events for project agent role {ProjectAgentRoleId} after {ElapsedMs}ms", projectAgentRoleId, elapsed);
            throw;
        }
    }

    /// <summary>
    /// 向项目智能体发送私聊消息。
    /// Sends a direct message to a project agent.
    /// </summary>
    public async Task<ConversationTaskOutput> SendMessageAsync(Guid projectId, Guid agentId, SendMessageRequest request, CancellationToken ct)
    {
        // zh-CN: 生产链路优先走统一入口服务，把“项目成员私聊”收敛成明确入口类型；
        // 旧实现只作为未注入统一入口时的兼容后备，避免现有隔离测试一次性全部失效。
        // en: Prefer the unified entry service in production while retaining the legacy path as a compatibility fallback.
        if (_conversationEntryService != null)
        {
                return await _conversationEntryService.SendProjectAgentPrivateAsync(
                    new ProjectAgentPrivateEntry(projectId, agentId, request.Content),
                    ct);
            }

        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogInformation("Sending message to project agent {AgentId} in project {ProjectId}", agentId, projectId);

            var projectAgent = await _projectAgents
                .AsNoTracking()
                .Include(item => item.AgentRole)
                .FirstOrDefaultAsync(item => item.Id == agentId && item.ProjectId == projectId, ct)
                ?? throw new KeyNotFoundException($"Project agent '{agentId}' was not found in project '{projectId}'.");

            if (projectAgent.AgentRole == null)
                throw new InvalidOperationException($"Project agent '{agentId}' does not have a role.");

            var runtimeResponse = await _agentService.CreateMessageAsync(
                new CreateMessageRequest(
                    Scene: MessageScene.Private,
                    MessageContext: new MessageContext(
                        ProjectId: projectId,
                        SessionId: null,
                        ParentMessageId: null,
                        FrameId: null,
                        ParentFrameId: null,
                        TaskId: null,
                        ProjectAgentRoleId: agentId,
                        TargetRole: AgentJobTitleCatalog.NormalizeKey(projectAgent.AgentRole.JobTitle) ?? projectAgent.AgentRole.Name,
                        InitiatorRole: MessageRoles.User,
                        Extra: null),
                    InputRole: ChatRole.User,
                    Input: request.Content),
                ct);

            if (!_agentService.TryGetMessageHandler(runtimeResponse.MessageId, out var handler) || handler == null)
                throw new InvalidOperationException($"Message handler '{runtimeResponse.MessageId}' was not created.");

            try
            {
                var summary = await handler.Completion;
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogInformation(
                    "Project agent {AgentId} completed in {ElapsedMs}ms with success: {Success}",
                    agentId,
                    elapsed,
                    summary.Success);

                return new ConversationTaskOutput
                {
                    TaskId = runtimeResponse.MessageId,
                    Status = summary.Success ? ExecutionPackageStatus.Completed : ExecutionPackageStatus.Failed,
                    ProjectId = projectId,
                    Scene = SessionSceneTypes.Private,
                    EntryKind = ExecutionEntryKinds.ProjectAgentPrivate,
                    ProjectAgentRoleId = agentId,
                    IsAwaitingInput = false
                };
            }
            finally
            {
                _agentService.RemoveMessageHandler(runtimeResponse.MessageId);
            }
        }
        catch (Exception ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Error sending message to project agent {AgentId} after {ElapsedMs}ms", agentId, elapsed);
            throw;
        }
    }

}

/// <summary>
/// 项目智能体私聊消息请求。
/// Request used to send a direct message to a project agent.
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// 消息正文。
    /// Message body.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 消息类型扩展字段。
    /// Optional message type hint.
    /// </summary>
    public string? MessageType { get; set; }
}

