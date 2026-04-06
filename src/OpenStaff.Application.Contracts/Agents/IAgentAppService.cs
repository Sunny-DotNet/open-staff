using OpenStaff.Application.Contracts.Agents.Dtos;

namespace OpenStaff.Application.Contracts.Agents;

public interface IAgentAppService
{
    Task<List<AgentDto>> GetProjectAgentsAsync(Guid projectId, CancellationToken ct = default);
    Task SetProjectAgentsAsync(Guid projectId, List<Guid> agentRoleIds, CancellationToken ct = default);
    Task<PagedAgentEventsDto> GetEventsAsync(Guid projectId, Guid agentId, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task SendMessageAsync(Guid projectId, Guid agentId, string message, CancellationToken ct = default);
}
