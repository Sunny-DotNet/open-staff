using OpenStaff.Application.Contracts.Common;
using OpenStaff.Application.Contracts.Monitor.Dtos;

namespace OpenStaff.Application.Contracts.Monitor;

public interface IMonitorAppService
{
    Task<SystemStatsDto> GetStatsAsync(CancellationToken ct = default);
    Task<ProjectStatsDto> GetProjectStatsAsync(Guid projectId, CancellationToken ct = default);
    Task<PagedResult<EventDto>> GetEventsAsync(GetEventsRequest request, CancellationToken ct = default);
}
