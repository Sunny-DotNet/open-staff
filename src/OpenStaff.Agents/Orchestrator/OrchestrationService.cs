using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Core.Notifications;
using OpenStaff.Core.Orchestration;

namespace OpenStaff.Agents.Orchestrator;

/// <summary>
/// 编排服务 — 管理项目智能体的创建和消息路由
/// Orchestration service — manages project agent lifecycle and message routing
/// </summary>
public class OrchestrationService : IOrchestrator
{
    private readonly AgentFactory _agentFactory;
    private readonly IProviderResolver _providerResolver;
    private readonly INotificationService _notification;
    private readonly ILogger<OrchestrationService> _logger;

    // 每个项目的智能体实例池 / Agent instance pool per project
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, (IAgent agent, DateTime lastUsed)>> _projectAgents = new();

    // 清理间隔 (1小时)
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

    public async Task<AgentResponse> HandleUserInputAsync(Guid projectId, string input, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling user input for project {ProjectId}", projectId);

        // 使用 orchestrator 角色决定路由 / Use orchestrator role for routing decision
        var routingAgent = await GetOrCreateAgentAsync(projectId, "orchestrator");
        if (routingAgent == null)
        {
            // 无 orchestrator 角色，默认路由到 communicator
            return await RouteToAgentAsync(projectId, BuiltinRoleTypes.Communicator,
                new AgentMessage { Content = input, FromRole = "user", Timestamp = DateTime.UtcNow }, cancellationToken);
        }

        var routingMessage = new AgentMessage
        {
            Content = input,
            FromRole = "user",
            Timestamp = DateTime.UtcNow
        };

        var routingResult = await routingAgent.ProcessAsync(routingMessage, cancellationToken);

        // 解析路由结果 / Parse routing result
        var targetRole = ParseRoutingTarget(routingResult.Content) ?? BuiltinRoleTypes.Communicator;

        var preview = input.Length > 50 ? input.Substring(0, 50) + "..." : input;
        await _notification.NotifyAsync(Channels.Project(projectId), EventTypes.Decision, new
        {
            content = $"路由到 {targetRole}: {preview}"
        }, cancellationToken);

        // 路由到目标角色 / Route to target role
        return await RouteToAgentAsync(projectId, targetRole,
            new AgentMessage { Content = input, FromRole = "user", Timestamp = DateTime.UtcNow }, cancellationToken);
    }

    public async Task<AgentResponse> RouteToAgentAsync(Guid projectId, string targetRole,
        AgentMessage message, CancellationToken cancellationToken = default)
    {
        var agent = await GetOrCreateAgentAsync(projectId, targetRole);
        if (agent == null)
        {
            return new AgentResponse
            {
                Success = false,
                Content = $"角色 {targetRole} 未注册",
                Errors = new List<string> { $"Role '{targetRole}' is not registered" }
            };
        }

        _logger.LogInformation("Routing to {Role} for project {ProjectId}", targetRole, projectId);
        return await agent.ProcessAsync(message, cancellationToken);
    }

    public async Task InitializeProjectAgentsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing agents for project {ProjectId}", projectId);

        var agents = _projectAgents.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, (IAgent, DateTime)>());

        foreach (var roleType in _agentFactory.RegisteredRoleTypes)
        {
            if (roleType == BuiltinRoleTypes.Orchestrator) continue; // orchestrator 按需创建
            await GetOrCreateAgentAsync(projectId, roleType);
        }

        await _notification.NotifyAsync(Channels.Project(projectId), EventTypes.SystemNotice, new
        {
            content = $"已初始化 {agents.Count} 个智能体角色"
        }, cancellationToken);

        // 触发清理
        _ = Task.Run(() => CleanupInactiveAgentsAsync(cancellationToken));
    }

    public Task<IReadOnlyList<AgentStatusInfo>> GetAgentStatusesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var result = new List<AgentStatusInfo>();

        if (_projectAgents.TryGetValue(projectId, out var agents))
        {
            foreach (var (roleType, agentData) in agents)
            {
                var config = _agentFactory.GetRoleConfig(roleType);
                result.Add(new AgentStatusInfo
                {
                    RoleType = roleType,
                    RoleName = config?.Name ?? roleType,
                    Status = agentData.agent.Status,
                    LastUsed = agentData.lastUsed
                });
            }
        }

        return Task.FromResult<IReadOnlyList<AgentStatusInfo>>(result);
    }

    private async Task<IAgent?> GetOrCreateAgentAsync(Guid projectId, string roleType)
    {
        if (!_agentFactory.IsRegistered(roleType))
        {
            _logger.LogWarning("Role type {RoleType} is not registered", roleType);
            return null;
        }

        var agents = _projectAgents.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, (IAgent, DateTime)>());

        if (agents.TryGetValue(roleType, out var existingData))
        {
            // 更新最后使用时间
            agents.TryUpdate(roleType, existingData, (existingData.agent, DateTime.UtcNow));
            return existingData.agent;
        }

        var agent = _agentFactory.CreateAgent(roleType);
        if (agent == null)
        {
            _logger.LogError("Failed to create agent for role type {RoleType}", roleType);
            return null;
        }

        var config = _agentFactory.GetRoleConfig(roleType);
        if (config == null)
        {
            _logger.LogWarning("Configuration not found for role type {RoleType}", roleType);
        }

        // 解析供应商和 API Key / Resolve provider and API key
        ModelProvider? provider = null;
        string? apiKey = null;
        var roleDb = _agentFactory.GetDbRole(roleType);
        if (roleDb?.ModelProviderId != null)
        {
            var resolved = await _providerResolver.ResolveAsync(roleDb.ModelProviderId.Value);
            if (resolved != null)
            {
                provider = resolved.Provider;
                apiKey = resolved.ApiKey;
            }
        }

        var context = new AgentContext
        {
            ProjectId = projectId,
            AgentInstanceId = Guid.NewGuid(),
            Role = new AgentRole
            {
                RoleType = roleType,
                Name = config?.Name ?? roleType,
                ModelName = config?.ModelName
            },
            Project = new Project { Id = projectId },
            Provider = provider,
            ApiKey = apiKey,
            NotificationService = _notification,
            Language = "zh-CN"
        };

        try
        {
            await agent.InitializeAsync(context);
            agents.TryAdd(roleType, (agent, DateTime.UtcNow));
            _logger.LogDebug("Created agent {RoleType} for project {ProjectId}", roleType, projectId);
            return agent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize agent {RoleType} for project {ProjectId}", roleType, projectId);
            return null;
        }
    }

    private string? ParseRoutingTarget(string? content)
    {
        if (string.IsNullOrEmpty(content)) return null;

        try
        {
            // Try to extract JSON from response
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
            _logger.LogWarning(ex, "Failed to parse routing response, defaulting to communicator");
        }

        return null;
    }

    /// <summary>
    /// 清理不活跃的智能体实例
    /// </summary>
    private async Task CleanupInactiveAgentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTime.UtcNow;
            var cutoffTime = now - _cleanupInterval;

            foreach (var (projectId, agents) in _projectAgents)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var toRemove = new List<string>();

                foreach (var (roleType, agentData) in agents)
                {
                    if (agentData.lastUsed < cutoffTime)
                    {
                        toRemove.Add(roleType);
                        _logger.LogDebug("Cleaning up inactive agent {RoleType} for project {ProjectId}", roleType, projectId);
                    }
                }

                foreach (var roleType in toRemove)
                {
                    if (agents.TryRemove(roleType, out var removedAgent))
                    {
                        // 这里可以添加智能体的清理逻辑
                        _logger.LogInformation("Removed inactive agent {RoleType} for project {ProjectId}", roleType, projectId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during agent cleanup");
        }
    }
}
