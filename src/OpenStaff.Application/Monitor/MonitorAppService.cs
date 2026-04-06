using Microsoft.EntityFrameworkCore;
using OpenStaff.Application.Contracts.Common;
using OpenStaff.Application.Contracts.Monitor;
using OpenStaff.Application.Contracts.Monitor.Dtos;
using OpenStaff.Application.Providers;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Application.Monitor;

public class MonitorAppService : IMonitorAppService
{
    private readonly AppDbContext _db;
    private readonly ProviderAccountService _accountService;

    public MonitorAppService(AppDbContext db, ProviderAccountService accountService)
    {
        _db = db;
        _accountService = accountService;
    }

    public async Task<SystemStatsDto> GetStatsAsync(CancellationToken ct)
    {
        var projectCount = await _db.Projects.CountAsync(ct);
        var agentCount = await _db.ProjectAgents.CountAsync(ct);
        var taskCount = await _db.Tasks.CountAsync(ct);
        var eventCount = await _db.AgentEvents.CountAsync(ct);
        var completedTasks = await _db.Tasks.CountAsync(t => t.Status == "done", ct);
        var sessionCount = await _db.ChatSessions.CountAsync(ct);
        var providerCount = (await _accountService.GetAllAsync()).Count;
        var agentRoleCount = await _db.AgentRoles.CountAsync(ct);

        var recentSessions = await _db.ChatSessions
            .OrderByDescending(s => s.CreatedAt)
            .Take(10)
            .Select(s => new RecentSessionDto
            {
                Id = s.Id,
                Status = s.Status,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(ct);

        return new SystemStatsDto
        {
            Projects = projectCount,
            Agents = agentCount,
            AgentRoles = agentRoleCount,
            Tasks = taskCount,
            CompletedTasks = completedTasks,
            Events = eventCount,
            Sessions = sessionCount,
            ModelProviders = providerCount,
            RecentSessions = recentSessions
        };
    }

    public async Task<ProjectStatsDto> GetProjectStatsAsync(Guid projectId, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) throw new KeyNotFoundException("Project not found");

        var agents = await _db.ProjectAgents
            .Where(a => a.ProjectId == projectId)
            .Include(a => a.AgentRole)
            .Select(a => new ProjectAgentDto
            {
                Id = a.Id,
                RoleName = a.AgentRole!.Name,
                Status = a.Status
            })
            .ToListAsync(ct);

        var tasksByStatus = await _db.Tasks
            .Where(t => t.ProjectId == projectId)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var recentEvents = await _db.AgentEvents
            .Where(e => e.ProjectId == projectId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(20)
            .Select(e => new EventDto
            {
                Id = e.Id,
                EventType = e.EventType,
                Data = e.Content,
                AgentId = e.AgentId,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(ct);

        var checkpointCount = await _db.Checkpoints
            .CountAsync(c => c.ProjectId == projectId, ct);

        return new ProjectStatsDto
        {
            Agents = agents,
            TasksByStatus = tasksByStatus.ToDictionary(x => x.Status, x => x.Count),
            RecentEvents = recentEvents,
            Checkpoints = checkpointCount
        };
    }

    public async Task<PagedResult<EventDto>> GetEventsAsync(GetEventsRequest request, CancellationToken ct)
    {
        var query = _db.AgentEvents.Where(e => e.ProjectId == request.ProjectId);

        if (!string.IsNullOrEmpty(request.EventType))
            query = query.Where(e => e.EventType == request.EventType);

        var total = await query.CountAsync(ct);
        var events = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new EventDto
            {
                Id = e.Id,
                EventType = e.EventType,
                Data = e.Content,
                AgentId = e.AgentId,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<EventDto>
        {
            Items = events,
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
