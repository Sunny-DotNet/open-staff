using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;

namespace OpenStaff.Agent;

/// <summary>
/// 智能体工厂 — 根据 ProviderType 路由到对应的 IAgentProvider 创建智能体
/// </summary>
public class AgentFactory
{
    private readonly Dictionary<string, IAgentProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public AgentFactory(IEnumerable<IAgentProvider> providers)
    {
        foreach (var provider in providers)
            _providers[provider.ProviderType] = provider;
    }

    /// <summary>根据数据库角色创建智能体</summary>
    public IAgent CreateAgent(AgentRole role)
    {
        var providerType = role.ProviderType ?? "builtin";
        if (!_providers.TryGetValue(providerType, out var provider))
            throw new InvalidOperationException($"Agent provider '{providerType}' is not registered");

        return provider.CreateAgent(role);
    }

    /// <summary>获取所有已注册的 Provider</summary>
    public IReadOnlyDictionary<string, IAgentProvider> Providers => _providers;

    /// <summary>检查 Provider 是否已注册</summary>
    public bool HasProvider(string providerType) => _providers.ContainsKey(providerType);
}
