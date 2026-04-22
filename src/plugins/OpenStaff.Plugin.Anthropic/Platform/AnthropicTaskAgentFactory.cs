using Anthropic;
using Anthropic.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenHub.Agents;
using OpenStaff.Agents;
using OpenStaff.Agent;
using OpenStaff.Core.Agents;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Plugin.Anthropic;

internal sealed class AnthropicTaskAgentFactory(
    IServiceProvider serviceProvider) : ITaskAgentFactory
{
    public async Task<OpenHub.Agents.ITaskAgent> CreateAsync(
        TaskAgentCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawContext = request.RawContext as AgentContext;
        var accountId = request.Role.ModelProviderId
            ?? throw new InvalidOperationException("Anthropic runtime requires a bound provider account.");
        dynamic protocol = ActivatorUtilities.CreateInstance<AnthropicProtocol>(serviceProvider);
        var configurationResult = await protocol.LoadConfigurationAsync(accountId.ToString(), cancellationToken);
        var configuration = (AnthropicProtocolEnv?)configurationResult.Configuration
            ?? throw new InvalidOperationException($"Provider account '{accountId}' is missing Anthropic configuration.");

        var client = new AnthropicClient
        {
            ApiKey = configuration.ApiKey,
            BaseUrl = configuration.BaseUrl!
        };

        // Anthropic 也走相同的 Skill 注入方式：目录 -> AIContextProvider，不是工具列表。
        var contextProviders = rawContext == null
            ? []
            : AgentSkillContextProviderFactory.CreateProviders(
                rawContext,
                serviceProvider,
                serviceProvider.GetRequiredService<ILoggerFactory>());
        var agentOptions = new ChatClientAgentOptions
        {
            Name = request.Role.Name,
            Description = request.Role.Description ?? request.Role.JobTitle,
            AIContextProviders = contextProviders.Count > 0 ? contextProviders : null
        };

        if (!string.IsNullOrWhiteSpace(request.Role.SystemPrompt))
        {
            agentOptions.ChatOptions ??= new();
            agentOptions.ChatOptions.Instructions = request.Role.SystemPrompt;
        }

        var agent = client.Beta.AsAIAgent(
            agentOptions,
            null,
            serviceProvider.GetRequiredService<ILoggerFactory>(),
            serviceProvider);

        return agent.AsTaskAgent().AsFeatureTaskAgent(serviceProvider, agent);
    }
}
