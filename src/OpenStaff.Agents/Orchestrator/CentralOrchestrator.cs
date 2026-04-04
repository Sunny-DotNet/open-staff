using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Core.Orchestration;
using OpenStaff.Core.Services;
using OpenStaff.Agents.Orchestrator.Prompts;

namespace OpenStaff.Agents.Orchestrator;

/// <summary>
/// 中央编排器 — 同时实现 IOrchestrator 和 IAgent（通过 AgentBase）
/// Central orchestrator — implements both IOrchestrator and IAgent (via AgentBase)
/// </summary>
public class CentralOrchestrator : AgentBase, IOrchestrator
{
    private readonly AgentFactory _agentFactory;

    /// <summary>
    /// 每个工程的活跃智能体实例 / Active agent instances per project
    /// Key: projectId, Value: { roleType → IAgent }
    /// </summary>
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, IAgent>> _projectAgents = new();

    /// <summary>
    /// 每个工程的智能体上下文 / Agent contexts per project (for status reporting)
    /// Key: projectId, Value: { roleType → AgentContext }
    /// </summary>
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, AgentContext>> _projectContexts = new();

    public override string RoleType => BuiltinRoleTypes.Orchestrator;

    public CentralOrchestrator(AgentFactory agentFactory, ILogger<CentralOrchestrator> logger)
        : base(logger)
    {
        _agentFactory = agentFactory;
    }

    // ────────────────────────── IOrchestrator ──────────────────────────

    /// <inheritdoc />
    public async Task<AgentResponse> HandleUserInputAsync(
        Guid projectId, string input, CancellationToken cancellationToken = default)
    {
        Status = AgentStatus.Thinking;

        // 发布用户输入事件 / Publish user-input event
        await PublishEventAsync(EventTypes.UserInput, input);

        try
        {
            // 使用 LLM 决定路由目标 / Use LLM to decide routing target
            var routing = await DecideRoutingAsync(projectId, input, cancellationToken);

            // 发布路由决策事件 / Publish routing decision event
            await PublishEventAsync(
                EventTypes.Decision,
                $"路由到 {routing.TargetRole} / Routed to {routing.TargetRole}",
                JsonSerializer.Serialize(routing));

            // 构建消息并路由 / Build message and route
            var message = new AgentMessage
            {
                FromRole = "user",
                Content = input,
                MessageType = "text"
            };

            var response = await RouteToAgentAsync(projectId, routing.TargetRole, message, cancellationToken);

            Status = AgentStatus.Idle;
            return response;
        }
        catch (Exception ex)
        {
            Status = AgentStatus.Error;
            Logger.LogError(ex, "处理用户输入失败 / Failed to handle user input");

            await PublishEventAsync(EventTypes.Error, ex.Message);

            return new AgentResponse
            {
                Success = false,
                Content = "处理请求时发生错误 / An error occurred while processing the request",
                Errors = { ex.Message }
            };
        }
    }

    /// <inheritdoc />
    public async Task<AgentResponse> RouteToAgentAsync(
        Guid projectId, string targetRole, AgentMessage message, CancellationToken cancellationToken = default)
    {
        var agent = GetOrCreateAgent(projectId, targetRole);
        if (agent is null)
        {
            Logger.LogWarning("未找到角色 {Role} 的智能体 / Agent not found for role {Role}", targetRole, targetRole);
            return new AgentResponse
            {
                Success = false,
                Content = $"未找到角色 {targetRole} 对应的智能体 / No agent found for role {targetRole}",
                Errors = { $"Agent for role '{targetRole}' is not registered or initialized" }
            };
        }

        // 发布消息路由事件 / Publish message routing event
        await PublishEventAsync(
            EventTypes.Action,
            $"消息已路由至 {targetRole} / Message routed to {targetRole}");

        return await agent.ProcessAsync(message, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentStatusInfo>> GetAgentStatusesAsync(
        Guid projectId, CancellationToken cancellationToken = default)
    {
        var result = new List<AgentStatusInfo>();

        if (_projectAgents.TryGetValue(projectId, out var agents))
        {
            _projectContexts.TryGetValue(projectId, out var contexts);

            foreach (var (roleType, agent) in agents)
            {
                var ctx = contexts is not null && contexts.TryGetValue(roleType, out var c) ? c : null;

                result.Add(new AgentStatusInfo
                {
                    AgentId = ctx?.AgentInstanceId ?? Guid.Empty,
                    RoleType = roleType,
                    RoleName = ctx?.Role?.Name ?? roleType,
                    Status = agent.Status,
                    CurrentTask = null
                });
            }
        }

        return Task.FromResult<IReadOnlyList<AgentStatusInfo>>(result);
    }

    /// <inheritdoc />
    public async Task InitializeProjectAgentsAsync(
        Guid projectId, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation(
            "初始化工程 {ProjectId} 的智能体 / Initializing agents for project {ProjectId}",
            projectId, projectId);

        // 确保编排器自身有上下文 / Ensure the orchestrator itself has a context
        if (Context is null)
        {
            Logger.LogWarning("编排器尚未初始化上下文 / Orchestrator context not initialized yet");
        }

        var agentDict = _projectAgents.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, IAgent>());
        var contextDict = _projectContexts.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, AgentContext>());

        // 为每个已注册的（非编排器）角色创建实例 / Create an instance for every registered (non-orchestrator) role
        foreach (var roleType in _agentFactory.RegisteredRoleTypes)
        {
            if (roleType == BuiltinRoleTypes.Orchestrator)
                continue;

            if (agentDict.ContainsKey(roleType))
                continue;

            var agent = _agentFactory.CreateAgent(roleType);
            var ctx = BuildAgentContext(projectId, roleType);

            await agent.InitializeAsync(ctx, cancellationToken);

            agentDict[roleType] = agent;
            contextDict[roleType] = ctx;
        }

        await PublishEventAsync(
            EventTypes.SystemNotice,
            $"工程 {projectId} 已初始化 {agentDict.Count} 个智能体 / " +
            $"Initialized {agentDict.Count} agents for project {projectId}");
    }

    // ────────────────────────── IAgent (AgentBase) ──────────────────────────

    /// <summary>
    /// 处理直接发给编排器的消息 / Handle messages directed at the orchestrator itself
    /// </summary>
    public override async Task<AgentResponse> ProcessAsync(
        AgentMessage message, CancellationToken cancellationToken = default)
    {
        // 编排器收到消息时按照 HandleUserInputAsync 的逻辑处理
        // When the orchestrator receives a message, delegate through HandleUserInputAsync
        var projectId = Context?.ProjectId ?? Guid.Empty;
        return await HandleUserInputAsync(projectId, message.Content, cancellationToken);
    }

    // ────────────────────────── 内部方法 / Internal helpers ──────────────────────────

    /// <summary>
    /// 使用 LLM 决定将用户输入路由到哪个角色 / Use LLM to decide routing target
    /// </summary>
    private async Task<RoutingDecision> DecideRoutingAsync(
        Guid projectId, string userInput, CancellationToken cancellationToken)
    {
        // 收集当前工程可用角色 / Gather available roles for this project
        var availableRoles = GetAvailableRoles(projectId);

        if (Context?.ModelClient is null)
        {
            Logger.LogWarning("ModelClient 未配置，使用默认路由 / ModelClient not configured, using default routing");
            return new RoutingDecision
            {
                TargetRole = BuiltinRoleTypes.Communicator,
                Reasoning = "ModelClient 不可用，默认路由到对话者 / ModelClient unavailable, defaulting to communicator",
                Priority = "normal"
            };
        }

        var request = new ChatRequest
        {
            Model = Context.Role?.ModelName ?? "gpt-4o-mini",
            Temperature = 0.3,
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = OrchestratorPrompts.SystemPrompt },
                new() { Role = "user", Content = OrchestratorPrompts.BuildRoutingPrompt(userInput, availableRoles) }
            }
        };

        await PublishEventAsync(EventTypes.Thought, "正在分析用户意图… / Analyzing user intent…");

        var response = await Context.ModelClient.ChatAsync(request, cancellationToken);

        return ParseRoutingDecision(response.Content, availableRoles);
    }

    /// <summary>
    /// 解析 LLM 的路由决策 JSON / Parse the routing-decision JSON from LLM output
    /// </summary>
    private RoutingDecision ParseRoutingDecision(string llmOutput, IReadOnlyList<string> availableRoles)
    {
        try
        {
            // 尝试从返回内容中提取 JSON / Try to extract JSON from response
            var json = ExtractJson(llmOutput);
            var decision = JsonSerializer.Deserialize<RoutingDecision>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (decision is not null && availableRoles.Contains(decision.TargetRole))
            {
                return decision;
            }
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "解析路由决策失败 / Failed to parse routing decision");
        }

        // 回退到默认角色 / Fall back to default role
        return new RoutingDecision
        {
            TargetRole = BuiltinRoleTypes.Communicator,
            Reasoning = "无法解析 LLM 输出，默认路由 / Could not parse LLM output, using default route",
            Priority = "normal"
        };
    }

    /// <summary>
    /// 从 LLM 输出中提取 JSON 块 / Extract a JSON block from LLM output
    /// </summary>
    private static string ExtractJson(string text)
    {
        // 处理 markdown 代码块 / Handle markdown code blocks
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start >= 0 && end > start)
        {
            return text[start..(end + 1)];
        }

        return text;
    }

    /// <summary>
    /// 获取或创建指定工程和角色的智能体 / Get or create an agent for the given project and role
    /// </summary>
    private IAgent? GetOrCreateAgent(Guid projectId, string roleType)
    {
        var agents = _projectAgents.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, IAgent>());

        if (agents.TryGetValue(roleType, out var existing))
            return existing;

        // 尝试通过工厂创建 / Try creating via factory
        if (!_agentFactory.IsRegistered(roleType))
            return null;

        var agent = _agentFactory.CreateAgent(roleType);
        var ctx = BuildAgentContext(projectId, roleType);

        agent.InitializeAsync(ctx).GetAwaiter().GetResult();

        agents[roleType] = agent;
        _projectContexts.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, AgentContext>())[roleType] = ctx;

        return agent;
    }

    /// <summary>
    /// 获取工程中可用的角色列表 / Get available roles for a project
    /// </summary>
    private IReadOnlyList<string> GetAvailableRoles(Guid projectId)
    {
        if (_projectAgents.TryGetValue(projectId, out var agents) && agents.Count > 0)
        {
            return agents.Keys.ToList();
        }

        // 返回已注册的所有非编排器角色 / Return all registered non-orchestrator roles
        return _agentFactory.RegisteredRoleTypes
            .Where(r => r != BuiltinRoleTypes.Orchestrator)
            .ToList();
    }

    /// <summary>
    /// 构建智能体上下文 / Build an agent context for the given project and role
    /// </summary>
    private AgentContext BuildAgentContext(Guid projectId, string roleType)
    {
        return new AgentContext
        {
            ProjectId = projectId,
            AgentInstanceId = Guid.NewGuid(),
            Role = new AgentRole
            {
                RoleType = roleType,
                Name = roleType,
                ModelName = Context?.Role?.ModelName
            },
            Project = Context?.Project ?? new Project { Id = projectId },
            ModelClient = Context?.ModelClient!,
            EventPublisher = Context?.EventPublisher!,
            Language = Context?.Language ?? "zh-CN"
        };
    }
}

/// <summary>
/// 路由决策结果 / Routing decision result
/// </summary>
internal class RoutingDecision
{
    public string TargetRole { get; set; } = BuiltinRoleTypes.Communicator;
    public string Reasoning { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
}
