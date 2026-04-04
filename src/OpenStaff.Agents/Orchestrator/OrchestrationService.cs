using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Events;
using OpenStaff.Core.Models;
using OpenStaff.Core.Orchestration;
using OpenStaff.Core.Services;

namespace OpenStaff.Agents.Orchestrator;

/// <summary>
/// 编排服务 — 管理项目智能体的创建和消息路由
/// Orchestration service — manages project agent lifecycle and message routing
/// </summary>
public class OrchestrationService : IOrchestrator
{
    private readonly AgentFactory _agentFactory;
    private readonly IModelClientFactory _modelClientFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<OrchestrationService> _logger;

    // 每个项目的智能体实例池 / Agent instance pool per project
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, IAgent>> _projectAgents = new();

    public OrchestrationService(
        AgentFactory agentFactory,
        IModelClientFactory modelClientFactory,
        IEventPublisher eventPublisher,
        ILogger<OrchestrationService> logger)
    {
        _agentFactory = agentFactory;
        _modelClientFactory = modelClientFactory;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<AgentResponse> HandleUserInputAsync(Guid projectId, string input, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling user input for project {ProjectId}", projectId);

        // 使用 orchestrator 角色决定路由 / Use orchestrator role for routing decision
        var routingAgent = GetOrCreateAgent(projectId, "orchestrator");
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
        await _eventPublisher.PublishAsync(new AgentEventData
        {
            ProjectId = projectId,
            EventType = EventTypes.Decision,
            Content = $"路由到 {targetRole}: {preview}"
        }, cancellationToken);

        // 路由到目标角色 / Route to target role
        return await RouteToAgentAsync(projectId, targetRole,
            new AgentMessage { Content = input, FromRole = "user", Timestamp = DateTime.UtcNow }, cancellationToken);
    }

    public async Task<AgentResponse> RouteToAgentAsync(Guid projectId, string targetRole,
        AgentMessage message, CancellationToken cancellationToken = default)
    {
        var agent = GetOrCreateAgent(projectId, targetRole);
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

        var agents = _projectAgents.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, IAgent>());

        foreach (var roleType in _agentFactory.RegisteredRoleTypes)
        {
            if (roleType == BuiltinRoleTypes.Orchestrator) continue; // orchestrator 按需创建
            GetOrCreateAgent(projectId, roleType);
        }

        await _eventPublisher.PublishAsync(new AgentEventData
        {
            ProjectId = projectId,
            EventType = EventTypes.SystemNotice,
            Content = $"已初始化 {agents.Count} 个智能体角色"
        }, cancellationToken);
    }

    public Task<IReadOnlyList<AgentStatusInfo>> GetAgentStatusesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var result = new List<AgentStatusInfo>();

        if (_projectAgents.TryGetValue(projectId, out var agents))
        {
            foreach (var (roleType, agent) in agents)
            {
                var config = _agentFactory.GetRoleConfig(roleType);
                result.Add(new AgentStatusInfo
                {
                    RoleType = roleType,
                    RoleName = config?.Name ?? roleType,
                    Status = agent.Status
                });
            }
        }

        return Task.FromResult<IReadOnlyList<AgentStatusInfo>>(result);
    }

    private IAgent? GetOrCreateAgent(Guid projectId, string roleType)
    {
        if (!_agentFactory.IsRegistered(roleType))
            return null;

        var agents = _projectAgents.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, IAgent>());

        return agents.GetOrAdd(roleType, rt =>
        {
            var agent = _agentFactory.CreateAgent(rt);
            var config = _agentFactory.GetRoleConfig(rt);

            // Initialize with context
            var context = new AgentContext
            {
                ProjectId = projectId,
                AgentInstanceId = Guid.NewGuid(),
                Role = new AgentRole
                {
                    RoleType = rt,
                    Name = config?.Name ?? rt,
                    ModelName = config?.ModelName
                },
                Project = new Project { Id = projectId },
                ModelClient = _modelClientFactory.CreateClient(new ModelProvider()),
                EventPublisher = _eventPublisher,
                Language = "zh-CN"
            };

            agent.InitializeAsync(context).GetAwaiter().GetResult();
            _logger.LogDebug("Created agent {RoleType} for project {ProjectId}", rt, projectId);
            return agent;
        });
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
}
