using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Contracts.Monitor;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/monitor")]
public class MonitorController : ControllerBase
{
    private readonly IMonitorAppService _monitorAppService;

    public MonitorController(IMonitorAppService monitorAppService)
    {
        _monitorAppService = monitorAppService;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"
        });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
        => Ok(await _monitorAppService.GetStatsAsync(ct));

    [HttpGet("projects/{projectId:guid}/stats")]
    public async Task<IActionResult> GetProjectStats(Guid projectId, CancellationToken ct)
        => Ok(await _monitorAppService.GetProjectStatsAsync(projectId, ct));

    [HttpGet("projects/{projectId:guid}/events")]
    public async Task<IActionResult> GetEvents(Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? eventType = null, CancellationToken ct = default)
        => Ok(await _monitorAppService.GetEventsAsync(projectId, page, pageSize, eventType, ct));
}
