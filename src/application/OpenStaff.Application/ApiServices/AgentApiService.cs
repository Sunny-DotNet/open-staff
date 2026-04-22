using Microsoft.Extensions.AI;
using OpenStaff.Agent;
using OpenStaff.Agent.Services;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Application.Agents.Services;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;

namespace OpenStaff.ApiServices;
/// <summary>
/// 项目智能体应用服务实现。
/// Application service implementation for project agent assignments and event access.
/// </summary>
public class AgentApiService : ApiServiceBase, IAgentApiService
{
    private readonly ProjectAgentService _agentService;
    private readonly ProjectService _projectService;
    private readonly IAgentPromptGenerator _agentPromptGenerator;
    private readonly IAgentMcpToolService _agentMcpToolService;
    private readonly IAgentSkillRuntimeService _agentSkillRuntimeService;

    /// <summary>
    /// 初始化项目智能体应用服务。
    /// Initializes the project agent application service.
    /// </summary>
    public AgentApiService(
        ProjectAgentService agentService,
        ProjectService projectService,
        IAgentPromptGenerator agentPromptGenerator,
        IAgentMcpToolService agentMcpToolService,
        IAgentSkillRuntimeService agentSkillRuntimeService,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _agentService = agentService;
        _projectService = projectService;
        _agentPromptGenerator = agentPromptGenerator;
        _agentMcpToolService = agentMcpToolService;
        _agentSkillRuntimeService = agentSkillRuntimeService;
    }

    /// <inheritdoc />
    public async Task<List<AgentDto>> GetProjectAgentsAsync(Guid projectId, CancellationToken ct)
    {
        var agents = await _projectService.GetProjectAgentsAsync(projectId, ct);
        return agents.Select(a => new AgentDto
        {
            Id = a.Id,
            ProjectId = a.ProjectId,
            AgentRoleId = a.AgentRoleId,
            RoleName = a.AgentRole?.Name,
            Status = a.Status,
            CurrentTask = a.CurrentTask,
            AgentRole = a.AgentRole == null
                ? null
                : new AgentRoleSummaryDto
                {
                    Id = a.AgentRole.Id,
                    Name = a.AgentRole.Name,
                    Description = a.AgentRole.Description
                }
        }).ToList();
    }

    /// <inheritdoc />
    public async Task SetProjectAgentsAsync(SetProjectAgentsRequest request, CancellationToken ct)
    {
        await _projectService.SetProjectAgentsAsync(request.ProjectId, request.AgentRoleIds, ct);
    }

    /// <inheritdoc />
    public async Task<PagedAgentEventsDto> GetEventsAsync(GetAgentEventsRequest request, CancellationToken ct)
    {
        var events = await _agentService.GetAgentEventsAsync(request.ProjectId, request.ProjectAgentRoleId, request.Page, request.PageSize, ct);
        return new PagedAgentEventsDto
        {
            Items = events.Select(MapEvent).ToList(),
            Total = events.Count
        };
    }

    /// <inheritdoc />
    public async Task<ConversationTaskOutput> SendMessageAsync(SendAgentMessageRequest request, CancellationToken ct)
    {
        var msg = new SendMessageRequest
        {
            Content = request.Message
        };
        return await _agentService.SendMessageAsync(request.ProjectId, request.ProjectAgentRoleId, msg, ct);
    }

    /// <inheritdoc />
    public async Task<AgentRuntimePreviewDto> GetRuntimePreviewAsync(Guid projectId, Guid projectAgentRoleId, CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException($"Project '{projectId}' was not found.");

        var projectAgent = (await _projectService.GetProjectAgentsAsync(projectId, ct))
            .FirstOrDefault(item => item.Id == projectAgentRoleId)
            ?? throw new KeyNotFoundException($"Project agent '{projectAgentRoleId}' was not found in project '{projectId}'.");

        var role = projectAgent.AgentRole
            ?? throw new InvalidOperationException($"Project agent '{projectAgentRoleId}' does not have a role.");

        var agentContext = new AgentContext
        {
            ProjectId = project.Id,
            Project = project,
            Language = project.Language ?? "zh-CN",
            Role = role,
            Scene = SceneType.ProjectGroup,
            AgentInstanceId = projectAgent.Id,
        };

        var prompt = await _agentPromptGenerator.PromptBuildAsync(role, agentContext, ct);
        var tools = BuildBuiltinRuntimeTools(role);

        var mcpTools = await _agentMcpToolService.LoadEnabledToolsAsync(
            new AgentMcpToolLoadContext(MessageScene.ProjectGroup, projectAgent.Id, AgentRoleId: null),
            ct);
        AppendMcpRuntimeTools(tools, mcpTools);

        var skillPayload = await _agentSkillRuntimeService.LoadRuntimePayloadAsync(
            new AgentSkillLoadContext(MessageScene.ProjectGroup, projectAgent.Id, AgentRoleId: null),
            ct);

        return new AgentRuntimePreviewDto
        {
            ProjectId = project.Id,
            ProjectAgentRoleId = projectAgent.Id,
            AgentRoleId = role.Id,
            RoleName = role.Name,
            JobTitle = role.JobTitle,
            Prompt = prompt,
            Tools = tools,
            Skills = skillPayload?.Skills
                .Select(item => new AgentRuntimeSkillDto
                {
                    InstallKey = item.InstallKey,
                    SkillId = item.SkillId,
                    DisplayName = item.DisplayName,
                    Source = item.Source,
                    DirectoryPath = item.DirectoryPath
                })
                .ToList()
                ?? [],
            MissingSkills = skillPayload?.MissingBindings
                .Select(item => new AgentRuntimeMissingSkillDto
                {
                    BindingScope = item.BindingScope,
                    SkillInstallKey = item.SkillInstallKey,
                    SkillId = item.SkillId,
                    DisplayName = item.DisplayName,
                    Message = item.Message
                })
                .ToList()
                ?? []
        };
    }

    private List<AgentRuntimeToolDto> BuildBuiltinRuntimeTools(AgentRole role)
    {
        return [];
    }

    private static void AppendMcpRuntimeTools(ICollection<AgentRuntimeToolDto> tools, IEnumerable<AITool> mcpTools)
    {
        var existingNames = new HashSet<string>(
            tools.Select(item => item.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var tool in mcpTools)
        {
            if (string.IsNullOrWhiteSpace(tool.Name) || !existingNames.Add(tool.Name))
                continue;

            tools.Add(new AgentRuntimeToolDto
            {
                Name = tool.Name,
                Source = "mcp"
            });
        }
    }

    /// <summary>
    /// 将智能体事件实体映射为包含运行时元数据的 DTO。
    /// Maps an agent-event entity to a DTO enriched with parsed runtime metadata.
    /// </summary>
    /// <param name="agentEvent">待映射的事件实体。/ Event entity to map.</param>
    /// <returns>供应用层返回的事件 DTO。/ The event DTO returned by the application layer.</returns>
    /// <remarks>
    /// zh-CN: 该方法会解析持久化的 Metadata JSON，并把可识别字段展开到 DTO，解析失败时保持对应字段为空。
    /// en: This method parses the persisted metadata JSON and expands recognized fields into the DTO, leaving fields empty when parsing yields no values.
    /// </remarks>
    private static AgentEventDto MapEvent(Entities.AgentEvent agentEvent)
    {
        var metadata = RuntimeProjectionMetadataMapper.ParseAgentEventMetadata(agentEvent.Metadata);
        return new AgentEventDto
        {
            Id = agentEvent.Id,
            EventType = agentEvent.EventType,
            Data = agentEvent.Content,
            Content = agentEvent.Content,
            Metadata = agentEvent.Metadata,
            TaskId = metadata?.TaskId,
            SessionId = metadata?.SessionId,
            FrameId = metadata?.FrameId,
            MessageId = metadata?.MessageId,
            ExecutionPackageId = metadata?.ExecutionPackageId,
            Scene = RuntimeProjectionMetadataMapper.NormalizeScene(metadata?.Scene),
            EntryKind = metadata?.EntryKind,
            AgentRoleId = metadata?.AgentRoleId,
            ProjectAgentRoleId = metadata?.ProjectAgentRoleId,
            TargetAgentRoleId = metadata?.TargetAgentRoleId,
            TargetProjectAgentRoleId = metadata?.TargetProjectAgentRoleId,
            Model = metadata?.Model,
            ToolName = metadata?.ToolName,
            ToolCallId = metadata?.ToolCallId,
            Status = metadata?.Status,
            SourceFrameId = metadata?.SourceFrameId,
            SourceEffectIndex = metadata?.SourceEffectIndex,
            Detail = metadata?.Detail,
            Attempt = metadata?.Attempt,
            MaxAttempts = metadata?.MaxAttempts,
            TotalTokens = metadata?.TotalTokens,
            DurationMs = metadata?.DurationMs,
            FirstTokenMs = metadata?.FirstTokenMs,
            CreatedAt = agentEvent.CreatedAt
        };
    }
}




