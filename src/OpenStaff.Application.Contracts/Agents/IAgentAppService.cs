using OpenStaff.Application.Contracts.Agents.Dtos;

namespace OpenStaff.Application.Contracts.Agents;

public interface IAgentAppService
{
    Task<List<AgentDto>> GetProjectAgentsAsync(Guid projectId, CancellationToken ct = default);
    Task SetProjectAgentsAsync(SetProjectAgentsRequest request, CancellationToken ct = default);
    Task<PagedAgentEventsDto> GetEventsAsync(GetAgentEventsRequest request, CancellationToken ct = default);
    Task SendMessageAsync(SendAgentMessageRequest request, CancellationToken ct = default);
}
