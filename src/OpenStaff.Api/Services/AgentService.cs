using Microsoft.EntityFrameworkCore;
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

    public AgentService(AppDbContext db, IOrchestrator orchestrator)
    {
        _db = db;
        _orchestrator = orchestrator;
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
        return await _db.AgentEvents
            .Where(e => e.ProjectId == projectId && e.AgentId == agentId)
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    /// <summary>
    /// 发送消息到智能体 — 通过 Orchestrator 路由
    /// Send message to agent — routed through Orchestrator
    /// </summary>
    public async Task<AgentResponse> SendMessageAsync(Guid projectId, Guid agentId, SendMessageRequest request, CancellationToken ct)
    {
        // 先确保项目智能体已初始化 / Ensure project agents are initialized
        var statuses = await _orchestrator.GetAgentStatusesAsync(projectId, ct);
        if (statuses.Count == 0)
        {
            await _orchestrator.InitializeProjectAgentsAsync(projectId, ct);
        }

        // 通过 Orchestrator 处理用户输入 / Route through orchestrator
        return await _orchestrator.HandleUserInputAsync(projectId, request.Content, ct);
    }
}

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public string? MessageType { get; set; }
}
