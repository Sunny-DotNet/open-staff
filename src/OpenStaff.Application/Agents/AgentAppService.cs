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

    public async Task SetProjectAgentsAsync(Guid projectId, List<Guid> agentRoleIds, CancellationToken ct)
    {
        await _projectService.SetProjectAgentsAsync(projectId, agentRoleIds, ct);
    }

    public async Task<PagedAgentEventsDto> GetEventsAsync(Guid projectId, Guid agentId, int page, int pageSize, CancellationToken ct)
    {
        var events = await _agentService.GetAgentEventsAsync(projectId, agentId, page, pageSize, ct);
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

    public async Task SendMessageAsync(Guid projectId, Guid agentId, string message, CancellationToken ct)
    {
        var request = new SendMessageRequest
        {
            Content = message
        };
        await _agentService.SendMessageAsync(projectId, agentId, request, ct);
    }
}
