using Microsoft.EntityFrameworkCore;
using OpenStaff.Application.Conversations.Models;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Conversations.Services;

/// <summary>
/// zh-CN: 系统触发型对话入口服务。
/// 它负责把“项目初始化后自动开场”“阶段切换后自动发提醒”“系统代某个角色抛出第一句”这类业务事件，
/// 统一收口成可复用的会话/消息落库逻辑，而不是让每个上层服务都手工拼 ChatSession / ChatFrame / ChatMessage。
/// en: Unified entry service for system-triggered conversation flows.
/// </summary>
public sealed class ConversationTriggerService
{
    private readonly IChatSessionRepository _chatSessions;
    private readonly IChatFrameRepository _chatFrames;
    private readonly IChatMessageRepository _chatMessages;
    private readonly IProjectAgentRoleRepository _projectAgents;
    private readonly IAgentRoleRepository _agentRoles;
    private readonly IRepositoryContext _repositoryContext;
    private readonly ILogger<ConversationTriggerService> _logger;

    public ConversationTriggerService(
        IChatSessionRepository chatSessions,
        IChatFrameRepository chatFrames,
        IChatMessageRepository chatMessages,
        IProjectAgentRoleRepository projectAgents,
        IAgentRoleRepository agentRoles,
        IRepositoryContext repositoryContext,
        ILogger<ConversationTriggerService> logger)
    {
        _chatSessions = chatSessions;
        _chatFrames = chatFrames;
        _chatMessages = chatMessages;
        _projectAgents = projectAgents;
        _agentRoles = agentRoles;
        _repositoryContext = repositoryContext;
        _logger = logger;
    }

    /// <summary>
    /// zh-CN: 触发项目场景中的一条系统消息。
    /// 该方法会按场景复用活跃会话，并根据幂等策略决定是否跳过已有消息的场景。
    /// en: Triggers a system-authored message within a project scene.
    /// </summary>
    public async Task<ProjectConversationTriggerResult> TriggerProjectSceneMessageAsync(
        ProjectConversationTriggerEntry entry,
        CancellationToken ct)
    {
        ValidateText(entry.Scene, nameof(entry.Scene));
        ValidateText(entry.SessionSummary, nameof(entry.SessionSummary));
        ValidateText(entry.FramePurpose, nameof(entry.FramePurpose));
        ValidateText(entry.MessageContent, nameof(entry.MessageContent));

        var normalizedScene = NormalizeScene(entry.Scene);
        var sceneSessions = await _chatSessions
            .AsNoTracking()
            .Where(session => session.ProjectId == entry.ProjectId && session.Scene == normalizedScene)
            .OrderByDescending(session => session.CreatedAt)
            .Select(session => new
            {
                session.Id,
                session.Status
            })
            .ToListAsync(ct);

        if (entry.IdempotencyMode == ConversationTriggerIdempotencyModes.SkipIfSceneHasMessages
            && sceneSessions.Count > 0)
        {
            var sceneSessionIds = sceneSessions.Select(session => session.Id).ToList();
            var existingMessage = await _chatMessages
                .AsNoTracking()
                .Where(message => sceneSessionIds.Contains(message.SessionId))
                .OrderByDescending(message => message.CreatedAt)
                .Select(message => new
                {
                    message.SessionId,
                    message.FrameId,
                    message.Id
                })
                .FirstOrDefaultAsync(ct);
            if (existingMessage != null)
            {
                _logger.LogDebug(
                    "Skipping conversation trigger for project {ProjectId} scene {Scene} because messages already exist in session {SessionId}",
                    entry.ProjectId,
                    normalizedScene,
                    existingMessage.SessionId);

                return new ProjectConversationTriggerResult(
                    existingMessage.SessionId,
                    existingMessage.FrameId,
                    existingMessage.Id,
                    CreatedSession: false,
                    CreatedMessage: false,
                    Skipped: true);
            }
        }

        var activeSessionId = sceneSessions
            .FirstOrDefault(session => session.Status == SessionStatus.Active || session.Status == SessionStatus.AwaitingInput)
            ?.Id;

        var createdSession = false;
        ChatSession session;
        if (activeSessionId.HasValue)
        {
            session = await _chatSessions.FirstAsync(item => item.Id == activeSessionId.Value, ct);
        }
        else
        {
            session = new ChatSession
            {
                ProjectId = entry.ProjectId,
                Scene = normalizedScene,
                Status = SessionStatus.Active,
                ContextStrategy = entry.ContextStrategy,
                InitialInput = entry.SessionSummary
            };
            _chatSessions.Add(session);
            createdSession = true;
        }

        var now = DateTime.UtcNow;
        var (agentRoleId, projectAgentRoleId) = await ResolveAuthorIdentityAsync(entry.ProjectId, entry.AuthorRole, ct);
        var frame = new ChatFrame
        {
            SessionId = session.Id,
            Depth = 0,
            Status = FrameStatus.Completed,
            Purpose = entry.FramePurpose,
            Result = entry.MessageContent,
            CompletedAt = now,
            InitiatorAgentRoleId = agentRoleId,
            InitiatorProjectAgentRoleId = projectAgentRoleId
        };
        _chatFrames.Add(frame);

        var message = new Entities.ChatMessage
        {
            SessionId = session.Id,
            FrameId = frame.Id,
            Role = entry.MessageRole,
            AgentRoleId = agentRoleId,
            ProjectAgentRoleId = projectAgentRoleId,
            Content = entry.MessageContent,
            ContentType = entry.MessageContentType,
            SequenceNo = 0
        };
        _chatMessages.Add(message);

        await _repositoryContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Triggered conversation message for project {ProjectId} scene {Scene}; session={SessionId}, message={MessageId}, createdSession={CreatedSession}",
            entry.ProjectId,
            normalizedScene,
            session.Id,
            message.Id,
            createdSession);

        return new ProjectConversationTriggerResult(
            session.Id,
            frame.Id,
            message.Id,
            CreatedSession: createdSession,
            CreatedMessage: true,
            Skipped: false);
    }

    private async Task<(Guid? AgentRoleId, Guid? ProjectAgentRoleId)> ResolveAuthorIdentityAsync(
        Guid projectId,
        string? authorRole,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(authorRole))
            return (null, null);

        var normalizedRole = authorRole.Trim();
        var projectRoles = await _projectAgents
            .AsNoTracking()
            .Include(item => item.AgentRole)
            .Where(item => item.ProjectId == projectId && item.AgentRole != null && item.AgentRole.IsActive)
            .OrderByDescending(item => item.AgentRole!.IsBuiltin)
            .ThenBy(item => item.CreatedAt)
            .ToListAsync(ct);

        var projectRole = projectRoles.FirstOrDefault(item => MatchesRoleDisplay(item.AgentRole!, normalizedRole));
        if (projectRole != null)
            return (projectRole.AgentRoleId, projectRole.Id);

        var globalRoles = await _agentRoles
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.IsBuiltin)
            .ThenBy(item => item.CreatedAt)
            .ToListAsync(ct);

        var globalRole = globalRoles.FirstOrDefault(item => MatchesRoleDisplay(item, normalizedRole));
        return globalRole == null ? (null, null) : (globalRole.Id, null);
    }

    private static bool MatchesRoleDisplay(AgentRole role, string authorRole)
    {
        return string.Equals(role.Name, authorRole, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                AgentJobTitleCatalog.NormalizeKey(role.JobTitle),
                AgentJobTitleCatalog.NormalizeKey(authorRole),
                StringComparison.OrdinalIgnoreCase)
            || (role.IsBuiltin && AgentJobTitleCatalog.IsSecretary(authorRole));
    }

    private static string NormalizeScene(string scene)
    {
        return SessionSceneTypes.TryParse(scene, out var parsed)
            ? parsed.ToString()
            : scene;
    }

    private static void ValidateText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} cannot be empty.", paramName);
    }
}
