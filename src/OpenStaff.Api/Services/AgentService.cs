using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Core.Orchestration;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Api.Services;

/// <summary>
/// 智能体应用服务 / Agent application service
/// </summary>
public class AgentService
{
    private readonly AppDbContext _db;
    private readonly IOrchestrator _orchestrator;
    private readonly ILogger<AgentService> _logger;

    public AgentService(AppDbContext db, IOrchestrator orchestrator, ILogger<AgentService> logger)
    {
        _db = db;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task<List<ProjectAgent>> GetProjectAgentsAsync(Guid projectId, CancellationToken ct)
    {
        return await _db.ProjectAgents
            .Where(a => a.ProjectId == projectId)
            .Include(a => a.AgentRole)
            .ToListAsync(ct);
    }

    public async Task<List<AgentEvent>> GetAgentEventsAsync(Guid projectId, Guid agentId, int page, int pageSize, CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20; // Limit page size

            _logger.LogDebug("Fetching events for agent {AgentId} in project {ProjectId}, page {Page}", agentId, projectId, page);

            var events = await _db.AgentEvents
                .Where(e => e.ProjectId == projectId && e.AgentId == agentId)
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("Retrieved {Count} events for agent {AgentId} in {ElapsedMs}ms", events.Count, agentId, elapsed);

            return events;
        }
        catch (Exception ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Error fetching events for agent {AgentId} after {ElapsedMs}ms", agentId, elapsed);
            throw;
        }
    }

    /// <summary>
    /// 发送消息到智能体 — 通过 Orchestrator 路由
    /// Send message to agent — routed through Orchestrator
    /// </summary>
    public async Task<AgentResponse> SendMessageAsync(Guid projectId, Guid agentId, SendMessageRequest request, CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogInformation("Sending message to agent {AgentId} in project {ProjectId}", agentId, projectId);

            // 先确保项目智能体已初始化 / Ensure project agents are initialized
            var statuses = await _orchestrator.GetAgentStatusesAsync(projectId, ct);
            if (statuses.Count == 0)
            {
                _logger.LogDebug("Initializing agents for project {ProjectId}", projectId);
                await _orchestrator.InitializeProjectAgentsAsync(projectId, ct);
            }

            // 通过 Orchestrator 处理用户输入 / Route through orchestrator
            var response = await _orchestrator.HandleUserInputAsync(projectId, request.Content, ct);

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("Agent {AgentId} responded in {ElapsedMs}ms with success: {Success}", agentId, elapsed, response.Success);

            return response;
        }
        catch (Exception ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Error sending message to agent {AgentId} after {ElapsedMs}ms", agentId, elapsed);
            throw;
        }
    }
}

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public string? MessageType { get; set; }
}
