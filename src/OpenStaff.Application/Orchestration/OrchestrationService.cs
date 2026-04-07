using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent;
using OpenStaff.Agent.Builtin;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Core.Notifications;
using OpenStaff.Core.Orchestration;

namespace OpenStaff.Application.Orchestration;

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

        // 使用 secretary 角色作为默认入口
        var secretaryRole = new AgentRole { RoleType = BuiltinRoleTypes.Secretary, Name = "Secretary" };
        var routingAgent = await GetOrCreateAgentAsync(projectId, secretaryRole);
        if (routingAgent == null)
        {
            return await RouteToAgentAsync(projectId, BuiltinRoleTypes.Secretary,
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
        var targetRole = ParseRoutingTarget(routingResult.Content) ?? BuiltinRoleTypes.Secretary;

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
        var role = new AgentRole { RoleType = targetRole, Name = targetRole };
        var agent = await GetOrCreateAgentAsync(projectId, role);
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

        // 初始化内置角色（secretary 按需创建）
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
                result.Add(new AgentStatusInfo
                {
                    RoleType = roleType,
                    RoleName = roleType,
                    Status = agentData.agent.Status,
                    LastUsed = agentData.lastUsed
                });
            }
        }

        return Task.FromResult<IReadOnlyList<AgentStatusInfo>>(result);
    }

    private async Task<IAgent?> GetOrCreateAgentAsync(Guid projectId, AgentRole role)
    {
        var roleType = role.RoleType;

        var agents = _projectAgents.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, (IAgent, DateTime)>());

        if (agents.TryGetValue(roleType, out var existingData))
        {
            agents.TryUpdate(roleType, existingData, (existingData.agent, DateTime.UtcNow));
            return existingData.agent;
        }

        try
        {
            var agent = _agentFactory.CreateAgent(role);

            // 解析供应商和 API Key
            ProviderAccount? account = null;
            string? apiKey = null;
            if (role.ModelProviderId != null)
            {
                var resolved = await _providerResolver.ResolveAsync(role.ModelProviderId.Value);
                if (resolved != null)
                {
                    account = resolved.Account;
                    apiKey = resolved.ApiKey;
                }
            }

            var context = new AgentContext
            {
                ProjectId = projectId,
                AgentInstanceId = Guid.NewGuid(),
                Role = role,
                Project = new Project { Id = projectId },
                Account = account,
                ApiKey = apiKey,
                NotificationService = _notification,
                Language = "zh-CN"
            };

            await agent.InitializeAsync(context);
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
