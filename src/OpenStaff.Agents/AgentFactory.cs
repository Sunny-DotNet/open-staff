using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;

namespace OpenStaff.Agents;

/// <summary>
/// 智能体工厂 — 从 RoleConfig 创建 StandardAgent / Agent factory — creates StandardAgent from RoleConfig
/// </summary>
public class AgentFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgentToolRegistry _toolRegistry;
    private readonly IPromptLoader _promptLoader;
    private readonly AIAgentFactory _aiAgentFactory;
    private readonly Dictionary<string, RoleConfig> _roleConfigs = new(StringComparer.OrdinalIgnoreCase);

    public AgentFactory(
        IServiceProvider serviceProvider,
        IAgentToolRegistry toolRegistry,
        IPromptLoader promptLoader,
        AIAgentFactory aiAgentFactory)
    {
        _serviceProvider = serviceProvider;
        _toolRegistry = toolRegistry;
        _promptLoader = promptLoader;
        _aiAgentFactory = aiAgentFactory;
    }

    /// <summary>注册角色配置 / Register role configuration</summary>
    public void RegisterRole(RoleConfig config)
    {
        _roleConfigs[config.RoleType] = config;
    }

    /// <summary>创建智能体实例 / Create agent instance</summary>
    public IAgent CreateAgent(string roleType)
    {
        if (!_roleConfigs.TryGetValue(roleType, out var config))
            throw new InvalidOperationException($"Role type '{roleType}' is not registered");

        var logger = _serviceProvider.GetRequiredService<ILogger<StandardAgent>>();
        return new StandardAgent(config, _toolRegistry, _promptLoader, _aiAgentFactory, logger);
    }

    /// <summary>检查角色是否已注册 / Check if role is registered</summary>
    public bool IsRegistered(string roleType) => _roleConfigs.ContainsKey(roleType);

    /// <summary>获取所有已注册角色类型 / Get all registered role types</summary>
    public IReadOnlyCollection<string> RegisteredRoleTypes => _roleConfigs.Keys.ToList();

    /// <summary>获取角色配置 / Get role configuration</summary>
    public RoleConfig? GetRoleConfig(string roleType) =>
        _roleConfigs.GetValueOrDefault(roleType);
}
