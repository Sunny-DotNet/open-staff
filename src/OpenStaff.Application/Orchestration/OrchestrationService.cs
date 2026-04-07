using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent;
using OpenStaff.Agent.Builtin;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Core.Notifications;
using OpenStaff.Core.Orchestration;
using OrchestratorResponse = OpenStaff.Core.Orchestration.OrchestrationResponse;

namespace OpenStaff.Application.Orchestration;

/// <summary>
/// 编排服务 — 管理项目智能体的创建和消息路由
/// </summary>
public class OrchestrationService : IOrchestrator
{
    private readonly AgentFactory _agentFactory;
    private readonly IProviderResolver _providerResolver;
    private readonly INotificationService _notification;
    private readonly ILogger<OrchestrationService> _logger;

    // 每个项目的智能体实例池
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, (AIAgent agent, DateTime lastUsed)>> _projectAgents = new();

    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);

    public OrchestrationService(
        AgentFactory agentFactory,
        IProviderResolver providerResolver,
        INotificationService notification,
        ILogger<OrchestrationService> logger)
    {
        _agentFactory = agentFactory;
        _providerResolver = providerResolver;
        _notification = notification;
        _logger = logger;
    }

    public async Task<OrchestratorResponse> HandleUserInputAsync(Guid projectId, string input, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling user input for project {ProjectId}", projectId);

        var secretaryRole = new AgentRole { RoleType = BuiltinRoleTypes.Secretary, Name = "Secretary" };
        var routingAgent = await GetOrCreateAgentAsync(projectId, secretaryRole);
        if (routingAgent == null)
        {
            return await RouteToAgentAsync(projectId, BuiltinRoleTypes.Secretary, input, cancellationToken);
        }

        var routingResult = await routingAgent.RunAsync(input, cancellationToken: cancellationToken);
        var routingContent = routingResult?.ToString();

        var targetRole = ParseRoutingTarget(routingContent) ?? BuiltinRoleTypes.Secretary;

        var preview = input.Length > 50 ? input[..50] + "..." : input;
        await _notification.NotifyAsync(Channels.Project(projectId), EventTypes.Decision, new
        {
            content = $"路由到 {targetRole}: {preview}"
        }, cancellationToken);

        return await RouteToAgentAsync(projectId, targetRole, input, cancellationToken);
    }

    public async Task<OrchestratorResponse> RouteToAgentAsync(Guid projectId, string targetRole,
        string message, CancellationToken cancellationToken = default)
    {
        var role = new AgentRole { RoleType = targetRole, Name = targetRole };
        var agent = await GetOrCreateAgentAsync(projectId, role);
        if (agent == null)
        {
            return new OrchestratorResponse
            {
                Success = false,
                Content = $"角色 {targetRole} 未注册",
                Errors = [$"Role '{targetRole}' is not registered"]
            };
        }

        _logger.LogInformation("Routing to {Role} for project {ProjectId}", targetRole, projectId);

        try
        {
            var result = await agent.RunAsync(message, cancellationToken: cancellationToken);
            var content = result?.ToString() ?? "";

            return new OrchestratorResponse
            {
                Success = true,
                Content = content,
                Data = new Dictionary<string, object>
                {
                    ["roleType"] = targetRole
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent {Role} processing failed", targetRole);
            return new OrchestratorResponse
            {
                Success = false,
                Content = $"处理失败: {ex.Message}",
                Errors = [ex.Message]
            };
        }
    }

    public async Task InitializeProjectAgentsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing agents for project {ProjectId}", projectId);

        var agents = _projectAgents.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, (AIAgent, DateTime)>());

        if (_agentFactory.Providers.TryGetValue("builtin", out var builtinProvider) && builtinProvider is BuiltinAgentProvider builtin)
        {
            foreach (var roleType in builtin.RoleConfigs.Keys)
            {
                if (roleType == BuiltinRoleTypes.Secretary) continue;
                var role = new AgentRole { RoleType = roleType, Name = roleType };
                await GetOrCreateAgentAsync(projectId, role);
            }
        }

        await _notification.NotifyAsync(Channels.Project(projectId), EventTypes.SystemNotice, new
        {
            content = $"已初始化 {agents.Count} 个智能体角色"
        }, cancellationToken);

        _ = Task.Run(() => CleanupInactiveAgentsAsync(cancellationToken));
    }

    public Task<IReadOnlyList<AgentStatusInfo>> GetAgentStatusesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var result = new List<AgentStatusInfo>();

        if (_projectAgents.TryGetValue(projectId, out var agents))
        {
            foreach (var (roleType, agentData) in agents)
            {
                result.Add(new AgentStatusInfo
                {
                    RoleType = roleType,
                    RoleName = roleType,
                    Status = AgentStatus.Idle,
                    LastUsed = agentData.lastUsed
                });
            }
        }

        return Task.FromResult<IReadOnlyList<AgentStatusInfo>>(result);
    }

    private async Task<AIAgent?> GetOrCreateAgentAsync(Guid projectId, AgentRole role)
    {
        var roleType = role.RoleType;

        var agents = _projectAgents.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, (AIAgent, DateTime)>());

        if (agents.TryGetValue(roleType, out var existingData))
        {
            agents.TryUpdate(roleType, (existingData.agent, DateTime.UtcNow), existingData);
            return existingData.agent;
        }

        try
        {
            // 解析供应商和 API Key
            ResolvedProvider? resolved = null;
            if (role.ModelProviderId != null)
            {
                resolved = await _providerResolver.ResolveAsync(role.ModelProviderId.Value);
            }

            if (resolved == null)
                throw new InvalidOperationException($"Cannot resolve provider for role '{roleType}'");

            var agent = _agentFactory.CreateAgent(role, resolved);

            agents.TryAdd(roleType, (agent, DateTime.UtcNow));
            _logger.LogDebug("Created agent {RoleType} for project {ProjectId}", roleType, projectId);
            return agent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create agent {RoleType} for project {ProjectId}", roleType, projectId);
            return null;
        }
    }

    private string? ParseRoutingTarget(string? content)
    {
        if (string.IsNullOrEmpty(content)) return null;

        try
        {
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("targetRole", out var target))
                {
                    return target.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse routing response, defaulting to secretary");
        }

        return null;
    }

    private async Task CleanupInactiveAgentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow - _cleanupInterval;

            foreach (var (projectId, agents) in _projectAgents)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var toRemove = agents
                    .Where(kv => kv.Value.lastUsed < cutoffTime)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var roleType in toRemove)
                {
                    if (agents.TryRemove(roleType, out _))
                        _logger.LogInformation("Removed inactive agent {RoleType} for project {ProjectId}", roleType, projectId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during agent cleanup");
        }
    }
}
