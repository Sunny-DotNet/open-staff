using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// 运行态监控应用服务契约。
/// Application service contract for runtime monitoring dashboards.
/// </summary>
public interface IMonitorApiService : IApiServiceBase
{
    /// <summary>
    /// 获取全局监控统计信息。
    /// Gets global monitoring statistics.
    /// </summary>
    Task<SystemStatsDto> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取单个项目的监控统计信息。
    /// Gets monitoring statistics for a single project.
    /// </summary>
    Task<ProjectStatsDto> GetProjectStatsAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// 分页获取项目事件流。
    /// Gets a paged event feed for a project.
    /// </summary>
    Task<PagedResult<EventDto>> GetEventsAsync(GetEventsRequest request, CancellationToken ct = default);
}


