using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// 性能监控控制器 / Performance monitoring controller
/// </summary>
[ApiController]
[Route("api/monitor")]
public class MonitorController : ControllerBase
{
    private readonly AppDbContext _db;

    public MonitorController(AppDbContext db) => _db = db;

    /// <summary>
    /// 系统健康检查 / System health check
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = typeof(MonitorController).Assembly.GetName().Version?.ToString() ?? "1.0.0"
        });
    }

    /// <summary>
    /// 系统概览统计 / System overview statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var projectCount = await _db.Projects.CountAsync(ct);
        var agentCount = await _db.ProjectAgents.CountAsync(ct);
        var taskCount = await _db.Tasks.CountAsync(ct);
        var eventCount = await _db.AgentEvents.CountAsync(ct);
        var completedTasks = await _db.Tasks.CountAsync(t => t.Status == "done", ct);
        var providerCount = await _db.ModelProviders.CountAsync(p => p.IsActive, ct);

        return Ok(new
        {
            projects = projectCount,
            agents = agentCount,
            tasks = new { total = taskCount, completed = completedTasks },
            events = eventCount,
            modelProviders = providerCount,
            uptime = Environment.TickCount64 / 1000
        });
    }

    /// <summary>
    /// 项目级别统计 / Project-level statistics
    /// </summary>
    [HttpGet("projects/{projectId:guid}/stats")]
    public async Task<IActionResult> GetProjectStats(Guid projectId, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound();

        var agents = await _db.ProjectAgents
            .Where(a => a.ProjectId == projectId)
            .Include(a => a.AgentRole)
            .Select(a => new { a.Id, Role = a.AgentRole!.Name, a.Status })
            .ToListAsync(ct);

        var tasks = await _db.Tasks
            .Where(t => t.ProjectId == projectId)
            .GroupBy(t => t.Status)
            .Select(g => new { status = g.Key, count = g.Count() })
            .ToListAsync(ct);

        var recentEvents = await _db.AgentEvents
            .Where(e => e.ProjectId == projectId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(20)
            .Select(e => new { e.EventType, e.Content, e.CreatedAt, e.AgentId })
            .ToListAsync(ct);

        var checkpointCount = await _db.Checkpoints
            .CountAsync(c => c.ProjectId == projectId, ct);

        return Ok(new
        {
            projectId,
            agents,
            tasks,
            recentEvents,
            checkpoints = checkpointCount
        });
    }

    /// <summary>
    /// 获取事件时间线 / Get event timeline
    /// </summary>
    [HttpGet("projects/{projectId:guid}/events")]
    public async Task<IActionResult> GetEvents(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? eventType = null,
        CancellationToken ct = default)
    {
        var query = _db.AgentEvents.Where(e => e.ProjectId == projectId);

        if (!string.IsNullOrEmpty(eventType))
            query = query.Where(e => e.EventType == eventType);

        var total = await query.CountAsync(ct);
        var events = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items = events });
    }
}
