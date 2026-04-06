using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Vendor;

namespace OpenStaff.Agents;

/// <summary>
/// 智能体工厂 — 根据 AgentSource 路由创建不同类型的智能体
/// </summary>
public class AgentFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgentToolRegistry _toolRegistry;
    private readonly AIAgentFactory _aiAgentFactory;
    private readonly Dictionary<string, RoleConfig> _roleConfigs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Core.Models.AgentRole> _dbRoles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IVendorAgentProvider> _vendorProviders = new(StringComparer.OrdinalIgnoreCase);

    public AgentFactory(
        IServiceProvider serviceProvider,
        IAgentToolRegistry toolRegistry,
        AIAgentFactory aiAgentFactory,
        IEnumerable<IVendorAgentProvider> vendorProviders)
    {
        _serviceProvider = serviceProvider;
        _toolRegistry = toolRegistry;
        _aiAgentFactory = aiAgentFactory;

        foreach (var provider in vendorProviders)
            _vendorProviders[provider.VendorType] = provider;
    }

    /// <summary>注册角色配置 / Register role configuration</summary>
    public void RegisterRole(RoleConfig config)
    {
        _roleConfigs[config.RoleType] = config;
    }

    /// <summary>注册数据库角色信息（含 ModelProviderId 等）/ Register DB role info</summary>
    public void RegisterDbRole(Core.Models.AgentRole role)
    {
        _dbRoles[role.RoleType] = role;
    }

    /// <summary>获取数据库角色信息 / Get DB role info</summary>
    public Core.Models.AgentRole? GetDbRole(string roleType) =>
        _dbRoles.GetValueOrDefault(roleType);

    /// <summary>创建智能体实例（Builtin/Custom 通过 RoleConfig）</summary>
    public IAgent CreateAgent(string roleType)
    {
        if (!_roleConfigs.TryGetValue(roleType, out var config))
            throw new InvalidOperationException($"Role type '{roleType}' is not registered");

        var logger = _serviceProvider.GetRequiredService<ILogger<StandardAgent>>();
        return new StandardAgent(config, _toolRegistry, _aiAgentFactory, logger);
    }

    /// <summary>
    /// 从数据库角色创建智能体 — 根据 Source 路由到不同的创建流程
    /// </summary>
    public IAgent CreateAgentFromDbRole(Core.Models.AgentRole dbRole)
    {
        // Vendor 类型：委托给对应的 VendorProvider
        if (dbRole.Source == AgentSource.Vendor && !string.IsNullOrEmpty(dbRole.VendorType))
        {
            return CreateVendorAgent(dbRole);
        }

        // Builtin / Custom：走 StandardAgent 流程
        RoleConfig config;

        if (_roleConfigs.TryGetValue(dbRole.RoleType, out var existingConfig))
        {
            config = existingConfig.Clone();
            if (!string.IsNullOrEmpty(dbRole.ModelName))
                config.ModelName = dbRole.ModelName;
            if (!string.IsNullOrEmpty(dbRole.SystemPrompt))
                config.SystemPrompt = dbRole.SystemPrompt;
        }
        else
        {
            config = BuildRoleConfigFromDb(dbRole);
        }

        var logger = _serviceProvider.GetRequiredService<ILogger<StandardAgent>>();
        return new StandardAgent(config, _toolRegistry, _aiAgentFactory, logger);
    }

    /// <summary>创建 Vendor 智能体</summary>
    private IAgent CreateVendorAgent(Core.Models.AgentRole dbRole)
    {
        if (!_vendorProviders.TryGetValue(dbRole.VendorType!, out var provider))
            throw new InvalidOperationException($"Vendor type '{dbRole.VendorType}' is not registered");

        // 从 Config JSON 解析 VendorConfig
        var vendorConfig = new VendorConfig();
        if (!string.IsNullOrEmpty(dbRole.Config))
        {
            try
            {
                var values = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(dbRole.Config);
                if (values != null)
                    vendorConfig.Values = values;
            }
            catch { /* ignore parse errors */ }
        }

        return provider.CreateAgent(dbRole, vendorConfig);
    }

    /// <summary>从数据库角色构建 RoleConfig</summary>
    private static RoleConfig BuildRoleConfigFromDb(Core.Models.AgentRole dbRole)
    {
        var config = new RoleConfig
        {
            RoleType = dbRole.RoleType,
            Name = dbRole.Name,
            Description = dbRole.Description,
            IsBuiltin = false,
            SystemPrompt = dbRole.SystemPrompt ?? string.Empty,
            ModelName = dbRole.ModelName,
        };

        if (!string.IsNullOrEmpty(dbRole.Config))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(dbRole.Config);
                var root = doc.RootElement;

                if (root.TryGetProperty("modelParameters", out var mp))
                {
                    config.ModelParameters = new ModelParameters
                    {
                        Temperature = mp.TryGetProperty("temperature", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.Number
                            ? t.GetDouble() : 0.7,
                        MaxTokens = mp.TryGetProperty("maxTokens", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.Number
                            ? m.GetInt32() : 4096,
                    };
                }

                if (root.TryGetProperty("tools", out var tools) && tools.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    config.Tools = tools.EnumerateArray()
                        .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .ToList();
                }
            }
            catch { /* ignore parse errors */ }
        }

        return config;
    }

    /// <summary>检查角色是否已注册 / Check if role is registered</summary>
    public bool IsRegistered(string roleType) => _roleConfigs.ContainsKey(roleType);

    /// <summary>获取所有已注册角色类型 / Get all registered role types</summary>
    public IReadOnlyCollection<string> RegisteredRoleTypes => _roleConfigs.Keys.ToList();

    /// <summary>获取角色配置 / Get role configuration</summary>
    public RoleConfig? GetRoleConfig(string roleType) =>
        _roleConfigs.GetValueOrDefault(roleType);

    /// <summary>获取所有已注册的 Vendor Provider</summary>
    public IReadOnlyDictionary<string, IVendorAgentProvider> VendorProviders => _vendorProviders;
}
