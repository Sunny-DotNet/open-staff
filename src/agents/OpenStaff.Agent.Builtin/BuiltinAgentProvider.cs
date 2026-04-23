using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agents;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Agent.Builtin;

/// <summary>
/// zh-CN: 为内置角色和自定义 OpenAI 兼容角色创建运行时智能体。
/// en: Creates runtime agents for builtin roles and custom OpenAI-compatible roles.
/// </summary>
public class BuiltinAgentProvider : IAgentProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ChatClientFactory _chatClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IAgentPromptGenerator _agentPromptGenerator;

    /// <summary>
    /// zh-CN: 使用工具上下文和聊天客户端依赖初始化内置提供程序。
    /// en: Initializes the builtin provider with tool-context and chat-client dependencies.
    /// </summary>
    public BuiltinAgentProvider(
        IServiceProvider serviceProvider,
        ChatClientFactory chatClientFactory,
        IAgentPromptGenerator agentPromptGenerator,
        ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _chatClientFactory = chatClientFactory;
        _agentPromptGenerator = agentPromptGenerator;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// zh-CN: 获取内置提供程序标识。
    /// en: Gets the builtin provider identifier.
    /// </summary>
    public string ProviderType => "builtin";

    /// <summary>
    /// zh-CN: 获取内置提供程序显示名称。
    /// en: Gets the builtin provider display name.
    /// </summary>
    public string DisplayName => "内置标准";

    /// <summary>
    /// zh-CN: 创建内置 Staff 智能体实例。
    /// en: Creates a builtin staff-agent instance.
    /// </summary>
    public async Task<IStaffAgent> CreateAgentAsync(AgentRole role, AgentContext context)
    {
        var components = await PrepareAgentAsync(role, context);
        return components.Agent.AsStaffAgent(_serviceProvider);
    }

    /// <summary>
    /// zh-CN: 创建带附加工具的内置智能体实例。
    /// en: Creates a builtin agent instance with additional runtime tools.
    /// </summary>
    public async Task<IStaffAgent> CreateAgentAsync(
        AgentRole role,
        AgentContext context,
        IList<AITool>? additionalTools)
    {
        var components = await PrepareAgentAsync(role, context, additionalTools);
        return components.Agent.AsStaffAgent(_serviceProvider);
    }

    /// <summary>
    /// zh-CN: 准备智能体、指令和工具等可复用组件。
    /// en: Prepares the reusable agent, instructions, and tools required for execution.
    /// </summary>
    public async Task<AgentComponents> PrepareAgentAsync(
        AgentRole role,
        AgentContext context,
        IList<AITool>? additionalTools = null)
    {
        // zh-CN: 运行时角色画像完全来自数据库实体；这里仅重建执行当前模型所需的轻量配置。
        // en: The runtime execution profile comes entirely from the persisted role entity; this only reconstructs the lightweight config needed for execution.
        var config = BuildRoleConfigFromDb(role);
        var modelName = config.ModelName ?? role.ModelName ?? "gpt-4o";
        if (!role.ModelProviderId.HasValue)
            throw new InvalidOperationException($"Role '{role.Name}' must bind a provider account.");

        var providerAccountId = role.ModelProviderId.Value;
        var projectGroupOutputMode = RequiresProjectGroupOrchestratorContract(role, context)
            ? ProjectGroupOrchestratorContract.TaggedJsonFallbackOutputMode
            : null;
        ResolvedProvider? resolvedProvider = null;

        if (projectGroupOutputMode != null)
        {
            (resolvedProvider, projectGroupOutputMode) = await ResolveProjectGroupProviderModeAsync(
                providerAccountId,
                modelName,
                projectGroupOutputMode,
                CancellationToken.None);
            context.ExtraConfig[ProjectGroupOrchestratorContract.OutputModeExtraConfigKey] = projectGroupOutputMode;
        }

        var systemPrompt = await _agentPromptGenerator.PromptBuildAsync(role, context, CancellationToken.None);
        var chatClient = resolvedProvider != null
            ? await _chatClientFactory.CreateAsync(resolvedProvider, modelName, CancellationToken.None)
            : await _chatClientFactory.CreateAsync(providerAccountId, modelName, CancellationToken.None);

        List<AITool>? aiTools = null;
        if (additionalTools is { Count: > 0 })
        {
            aiTools ??= [];
            foreach (var tool in additionalTools)
            {
                // 这里的 additionalTools 主要就是运行时加载出来的 MCP 工具。
                // 它们直接挂进 ChatOptions.Tools，因此后续可能出现在 toolCalls 里。
                if (aiTools.Any(existing => string.Equals(existing.Name, tool.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                aiTools.Add(tool);
            }
        }

        // Skill 不会进 aiTools，而是走 AIContextProviders。
        var contextProviders = AgentSkillContextProviderFactory.CreateProviders(context, _serviceProvider, _loggerFactory);
        var options = new ChatClientAgentOptions
        {
            Name = config.Name,
            Description = config.Description,
            AIContextProviders = contextProviders.Count > 0 ? contextProviders : null
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            options.ChatOptions ??= new();
            options.ChatOptions.Instructions = systemPrompt;
        }

        if (string.Equals(
                projectGroupOutputMode,
                ProjectGroupOrchestratorContract.NativeJsonSchemaOutputMode,
                StringComparison.Ordinal))
        {
            options.ChatOptions ??= new();
            options.ChatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(
                typeof(ProjectGroupOrchestratorResult),
                ProjectGroupOrchestratorContract.SerializerOptions,
                ProjectGroupOrchestratorContract.EnvelopeTag,
                "Structured project-group orchestration result.");
        }

        if (aiTools is { Count: > 0 })
        {
            options.ChatOptions ??= new();
            options.ChatOptions.Tools = aiTools;
        }

        AIAgent agent = chatClient.AsAIAgent(options, _loggerFactory, _serviceProvider);

        return new AgentComponents(agent, config.Name, systemPrompt, aiTools);
    }

    private async Task<(ResolvedProvider? Provider, string OutputMode)> ResolveProjectGroupProviderModeAsync(
        Guid providerAccountId,
        string modelName,
        string fallbackMode,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var providerResolver = scope.ServiceProvider.GetService<IProviderResolver>();
        var protocolFactory = scope.ServiceProvider.GetService<IProtocolFactory>();
        if (providerResolver == null || protocolFactory == null)
            return (null, fallbackMode);

        var resolvedProvider = await providerResolver.ResolveAsync(providerAccountId, cancellationToken);
        if (resolvedProvider == null)
            return (null, fallbackMode);

        var protocol = protocolFactory.CreateProtocolWithEnv(
            resolvedProvider.Account.ProtocolType,
            resolvedProvider.EnvConfigJson ?? "{}");
        var modelInfo = await _chatClientFactory.ResolveModelInfoAsync(
            resolvedProvider,
            protocol,
            modelName,
            cancellationToken);

        var outputMode = modelInfo.SupportsStructuredOutputs == true
            ? ProjectGroupOrchestratorContract.NativeJsonSchemaOutputMode
            : fallbackMode;
        return (resolvedProvider, outputMode);
    }

    private static bool RequiresProjectGroupOrchestratorContract(AgentRole role, AgentContext context)
    {
        return context.Scene == SceneType.ProjectGroup
            && (AgentJobTitleCatalog.IsSecretary(role.JobTitle) || AgentJobTitleCatalog.IsSecretary(role.Name));
    }

    /// <summary>
    /// zh-CN: 从数据库角色实体重建一份轻量运行时配置，并在可选 JSON 配置失效时保留核心身份信息继续执行。
    /// en: Reconstructs a lightweight runtime config from the persisted role entity and keeps the core identity usable even when optional JSON config can no longer be parsed.
    /// </summary>
    private static RoleConfig BuildRoleConfigFromDb(AgentRole dbRole)
    {
        var config = new RoleConfig
        {
            Name = dbRole.Name,
            Description = dbRole.Description,
            IsBuiltin = false,
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

            }
            catch
            {
                // zh-CN: 自定义角色配置允许渐进演化，解析失败时保留核心角色信息而不是阻断整个运行时创建。
                // en: Custom role config evolves over time, so keep the core role definition even if optional config JSON can no longer be parsed.
            }
        }

        return config;
    }
}
