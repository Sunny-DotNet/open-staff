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
    private readonly Dictionary<string, Core.Models.AgentRole> _dbRoles = new(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>注册数据库角色信息（含 ModelProviderId 等）/ Register DB role info</summary>
    public void RegisterDbRole(Core.Models.AgentRole role)
    {
        _dbRoles[role.RoleType] = role;
    }

    /// <summary>获取数据库角色信息 / Get DB role info</summary>
    public Core.Models.AgentRole? GetDbRole(string roleType) =>
        _dbRoles.GetValueOrDefault(roleType);

    /// <summary>创建智能体实例 / Create agent instance</summary>
    public IAgent CreateAgent(string roleType)
    {
        if (!_roleConfigs.TryGetValue(roleType, out var config))
            throw new InvalidOperationException($"Role type '{roleType}' is not registered");

        var logger = _serviceProvider.GetRequiredService<ILogger<StandardAgent>>();
        return new StandardAgent(config, _toolRegistry, _promptLoader, _aiAgentFactory, logger);
    }

    /// <summary>
    /// 从数据库角色动态创建智能体（用于自定义角色，无需预注册 RoleConfig）
    /// </summary>
    public IAgent CreateAgentFromDbRole(Core.Models.AgentRole dbRole)
    {
        // 优先使用已注册的 RoleConfig（内置角色）
        if (_roleConfigs.TryGetValue(dbRole.RoleType, out var existingConfig))
        {
            var logger1 = _serviceProvider.GetRequiredService<ILogger<StandardAgent>>();
            return new StandardAgent(existingConfig, _toolRegistry, _promptLoader, _aiAgentFactory, logger1);
        }

        // 从数据库角色动态构建 RoleConfig
        var config = BuildRoleConfigFromDb(dbRole);
        var logger = _serviceProvider.GetRequiredService<ILogger<StandardAgent>>();
        return new StandardAgent(config, _toolRegistry, _promptLoader, _aiAgentFactory, logger);
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

        // 解析 config JSON 中的 modelParameters 和 tools
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
}
