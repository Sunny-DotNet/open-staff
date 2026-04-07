using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent.Builtin.Prompts;
using OpenStaff.Agent.Builtin.Roles;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;

namespace OpenStaff.Agent.Builtin;

/// <summary>
/// 内置智能体供应商 — Builtin/Custom 类型角色统一走此 Provider
/// </summary>
public class BuiltinAgentProvider : IAgentProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgentToolRegistry _toolRegistry;
    private readonly AIAgentFactory _aiAgentFactory;
    private readonly IPromptLoader _promptLoader;
    private readonly Dictionary<string, RoleConfig> _roleConfigs = new(StringComparer.OrdinalIgnoreCase);

    public BuiltinAgentProvider(
        IServiceProvider serviceProvider,
        IAgentToolRegistry toolRegistry,
        AIAgentFactory aiAgentFactory,
        IPromptLoader promptLoader)
    {
        _serviceProvider = serviceProvider;
        _toolRegistry = toolRegistry;
        _aiAgentFactory = aiAgentFactory;
        _promptLoader = promptLoader;

        // 加载内置角色配置
        foreach (var config in RoleConfigLoader.LoadAll())
            _roleConfigs[config.RoleType] = config;
    }

    public string ProviderType => "builtin";
    public string DisplayName => "内置标准";

    public AgentConfigSchema GetConfigSchema() => new()
    {
        ProviderType = ProviderType,
        DisplayName = DisplayName,
        Description = "内置/自定义智能体，使用 OpenAI 兼容协议",
        Fields = []
    };

    public IAgent CreateAgent(AgentRole role)
    {
        RoleConfig config;

        if (_roleConfigs.TryGetValue(role.RoleType, out var existingConfig))
        {
            config = existingConfig.Clone();
            if (!string.IsNullOrEmpty(role.ModelName))
                config.ModelName = role.ModelName;
            if (!string.IsNullOrEmpty(role.SystemPrompt))
                config.SystemPrompt = role.SystemPrompt;
        }
        else
        {
            config = BuildRoleConfigFromDb(role);
        }

        var logger = _serviceProvider.GetRequiredService<ILogger<StandardAgent>>();
        return new StandardAgent(config, _toolRegistry, _aiAgentFactory, logger);
    }

    /// <summary>获取内置角色配置</summary>
    public RoleConfig? GetRoleConfig(string roleType) =>
        _roleConfigs.GetValueOrDefault(roleType);

    /// <summary>获取所有内置角色配置</summary>
    public IReadOnlyDictionary<string, RoleConfig> RoleConfigs => _roleConfigs;

    /// <summary>获取提示词加载器</summary>
    public IPromptLoader PromptLoader => _promptLoader;

    private static RoleConfig BuildRoleConfigFromDb(AgentRole dbRole)
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
}
