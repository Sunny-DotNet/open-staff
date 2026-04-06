using OpenStaff.Application.Contracts.Agents;
using OpenStaff.Application.Contracts.Agents.Dtos;
using OpenStaff.Application.Projects;

namespace OpenStaff.Application.Agents;

public class AgentAppService : IAgentAppService
{
    private readonly AgentService _agentService;
    private readonly ProjectService _projectService;

    public AgentAppService(AgentService agentService, ProjectService projectService)
    {
        _agentService = agentService;
        _projectService = projectService;
    }

    public async Task<List<AgentDto>> GetProjectAgentsAsync(Guid projectId, CancellationToken ct)
    {
        var agents = await _projectService.GetProjectAgentsAsync(projectId, ct);
        return agents.Select(a => new AgentDto
        {
            Id = a.Id,
            RoleType = a.AgentRole?.RoleType,
            RoleName = a.AgentRole?.Name,
            Status = a.Status
        }).ToList();
    }

    public async Task SetProjectAgentsAsync(SetProjectAgentsRequest request, CancellationToken ct)
    {
        await _projectService.SetProjectAgentsAsync(request.ProjectId, request.AgentRoleIds, ct);
    }

    public async Task<PagedAgentEventsDto> GetEventsAsync(GetAgentEventsRequest request, CancellationToken ct)
    {
        var events = await _agentService.GetAgentEventsAsync(request.ProjectId, request.AgentId, request.Page, request.PageSize, ct);
        return new PagedAgentEventsDto
        {
            Items = events.Select(e => new AgentEventDto
            {
                Id = e.Id,
                EventType = e.EventType,
                Data = e.Content,
                CreatedAt = e.CreatedAt
            }).ToList(),
            Total = events.Count
        };
    }

    public async Task SendMessageAsync(SendAgentMessageRequest request, CancellationToken ct)
    {
        var msg = new SendMessageRequest
        {
            Content = request.Message
        };
        await _agentService.SendMessageAsync(request.ProjectId, request.AgentId, msg, ct);
    }
}
