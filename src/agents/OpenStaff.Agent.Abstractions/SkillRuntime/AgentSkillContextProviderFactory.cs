using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;

namespace OpenStaff.Agent;

/// <summary>
/// zh-CN: 从 OpenStaff 的运行时 skill payload 创建原生 Agent Skills 上下文提供器。
/// en: Creates native Agent Skills context providers from the OpenStaff runtime skill payload.
/// </summary>
public static class AgentSkillContextProviderFactory
{
    /// <summary>
    /// zh-CN: 根据当前运行时上下文构建可注入到 <see cref="ChatClientAgentOptions" /> 的 AIContextProviders。
    /// en: Builds AIContextProviders that can be injected into <see cref="ChatClientAgentOptions" /> for the current runtime context.
    /// </summary>
    public static IReadOnlyList<AIContextProvider> CreateProviders(
        AgentContext context,
        IServiceProvider? serviceProvider = null,
        ILoggerFactory? loggerFactory = null)
    {
        // Skill 在当前系统里不是 AITool，而是“上下文提供器”。
        // 所以即使 Skill 正常注入，前端也不一定会看到 toolCalls。
        var skillPaths = context.GetResolvedSkillDirectories();
        if (skillPaths.Count == 0)
            return [];

        var scriptRunnerService = serviceProvider?.GetService<IAgentSkillScriptRunner>();
#pragma warning disable MAAI001
        AgentFileSkillScriptRunner? scriptRunner = scriptRunnerService is null
            ? null
            : scriptRunnerService.RunAsync;
#pragma warning restore MAAI001

#pragma warning disable MAAI001
        return
        [
            new AgentSkillsProvider(
                skillPaths,
                scriptRunner: scriptRunner,
                fileOptions: new AgentFileSkillsSourceOptions
                {
                    AllowedScriptExtensions = scriptRunnerService?.AllowedScriptExtensions?.Count > 0
                        ? [.. scriptRunnerService.AllowedScriptExtensions]
                        : []
                },
                loggerFactory: loggerFactory)
        ];
#pragma warning restore MAAI001
    }
}
