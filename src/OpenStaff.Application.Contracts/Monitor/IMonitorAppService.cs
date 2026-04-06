using OpenStaff.Application.Contracts.Common;
using OpenStaff.Application.Contracts.Monitor.Dtos;

namespace OpenStaff.Application.Contracts.Monitor;

public interface IMonitorAppService
{
    Task<SystemStatsDto> GetStatsAsync(CancellationToken ct = default);
    Task<ProjectStatsDto> GetProjectStatsAsync(Guid projectId, CancellationToken ct = default);
    Task<PagedResult<EventDto>> GetEventsAsync(Guid projectId, int page = 1, int pageSize = 50, string? eventType = null, CancellationToken ct = default);
}
