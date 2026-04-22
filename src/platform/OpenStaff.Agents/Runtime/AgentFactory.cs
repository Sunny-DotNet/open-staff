using OpenStaff.Agents;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Provider.Platforms;
namespace OpenStaff.Agent;

/// <summary>
/// zh-CN: 根据 ProviderType 将角色路由到对应的智能体提供程序。
/// en: Routes roles to the appropriate agent provider based on ProviderType.
/// </summary>
public class AgentFactory
{
    private readonly Dictionary<string, IAgentProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// zh-CN: 使用当前容器中可见的提供程序集合初始化工厂。
    /// en: Initializes the factory from the providers visible in the current container.
    /// </summary>
    public AgentFactory(IEnumerable<IAgentProvider> providers)
        : this(providers, new ServiceCollection().BuildServiceProvider())
    {
    }

    /// <summary>
    /// zh-CN: 使用当前容器中的统一运行时 Provider 集合初始化工厂。
    /// en: Initializes the factory from the unified runtime providers available in the current container.
    /// </summary>
    public AgentFactory(
        IEnumerable<IAgentProvider> providers,
        IServiceProvider serviceProvider)
    {
        foreach (var provider in providers)
            _providers[provider.ProviderType] = provider;
    }

    public AgentFactory(
        IPlatformRegistry platformRegistry,
        IEnumerable<IAgentProvider> providers,
        IServiceProvider serviceProvider)
        : this(providers, serviceProvider)
    {
    }

    /// <summary>
    /// zh-CN: 根据角色配置将运行时请求路由到唯一的智能体 Provider 合同。
    /// en: Routes runtime creation requests to the single agent-provider contract based on the role configuration.
    /// </summary>
    public async Task<IStaffAgent> CreateAgentAsync(AgentRole role, AgentContext context)
    {
        // zh-CN: 旧角色记录可能没有显式 ProviderType，这里保持 builtin 的兼容回退。
        // en: Legacy role records may omit ProviderType, so retain builtin as the backward-compatible fallback.
        var providerType = role.ProviderType ?? "builtin";

        if (!_providers.TryGetValue(providerType, out var agentProvider))
            throw new InvalidOperationException($"Agent provider '{providerType}' is not registered");

        return await agentProvider.CreateAgentAsync(role, context);
    }

    /// <summary>
    /// zh-CN: 获取当前容器中已注册的运行时提供程序。
    /// en: Gets all runtime providers currently registered in the container.
    /// </summary>
    public IReadOnlyDictionary<string, IAgentProvider> Providers => _providers;

    /// <summary>
    /// zh-CN: 检查指定提供程序是否已注册。
    /// en: Checks whether the specified provider is registered.
    /// </summary>
    public bool HasProvider(string providerType) => _providers.ContainsKey(providerType);
}
