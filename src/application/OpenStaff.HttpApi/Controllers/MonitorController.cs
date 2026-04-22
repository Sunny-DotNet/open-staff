
using Microsoft.AspNetCore.Mvc;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// 运行态监控控制器。
/// Controller that exposes runtime monitoring endpoints.
/// </summary>
[ApiController]
[Route("api/monitor")]
public class MonitorController : ControllerBase
{
    private readonly IMonitorApiService _monitorApiService;

    /// <summary>
    /// 初始化运行态监控控制器。
    /// Initializes the runtime monitor controller.
    /// </summary>
    public MonitorController(IMonitorApiService monitorApiService)
    {
        _monitorApiService = monitorApiService;
    }

    /// <summary>
    /// 返回基础健康状态。
    /// Returns a basic health payload.
    /// </summary>
    [HttpGet("health")]
    public ActionResult<HealthStatusDto> Health()
    {
        return Ok(new HealthStatusDto
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"
        });
    }

    /// <summary>
    /// 获取全局监控统计。
    /// Gets global monitoring statistics.
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<SystemStatsDto>> GetStats(CancellationToken ct)
        => Ok(await _monitorApiService.GetStatsAsync(ct));

    /// <summary>
    /// 获取单个项目的监控统计。
    /// Gets monitoring statistics for a project.
    /// </summary>
    [HttpGet("projects/{projectId:guid}/stats")]
    public async Task<ActionResult<ProjectStatsDto>> GetProjectStats(Guid projectId, CancellationToken ct)
        => Ok(await _monitorApiService.GetProjectStatsAsync(projectId, ct));

    /// <summary>
    /// 获取项目事件流。
    /// Gets the event feed for a project.
    /// </summary>
    [HttpGet("projects/{projectId:guid}/events")]
    public async Task<ActionResult<PagedResult<EventDto>>> GetEvents(Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? eventType = null, [FromQuery] string? scene = null, CancellationToken ct = default)
        => Ok(await _monitorApiService.GetEventsAsync(new GetEventsRequest
        {
            ProjectId = projectId,
            Page = page,
            PageSize = pageSize,
            EventType = eventType,
            Scene = scene
        }, ct));
}

