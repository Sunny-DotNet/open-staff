using Google.GenAI;
using Google.GenAI.Types;
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

namespace OpenStaff.Plugin.Google;

internal sealed class GoogleTaskAgentFactory(
    IServiceProvider serviceProvider,
    GoogleConfigurationService configurationService) : ITaskAgentFactory
{
    public async Task<OpenHub.Agents.ITaskAgent> CreateAsync(
        TaskAgentCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawContext = request.RawContext as AgentContext;
        var accountId = request.Role.ModelProviderId
            ?? throw new InvalidOperationException("Google runtime requires a bound provider account.");
        dynamic protocol = ActivatorUtilities.CreateInstance<GoogleProtocol>(serviceProvider);
        var configurationResult = await protocol.LoadConfigurationAsync(accountId.ToString(), cancellationToken);
        var configuration = (GoogleProtocolEnv?)configurationResult.Configuration
            ?? throw new InvalidOperationException($"Provider account '{accountId}' is missing Google configuration.");
        var platformConfiguration = (await configurationService.GetConfigurationAsync(cancellationToken)).Configuration;
        var apiKey = configuration.ApiKey;

        HttpOptions? httpOptions = null;
        if (!string.IsNullOrWhiteSpace(configuration.BaseUrl))
        {
            httpOptions = new HttpOptions
            {
                BaseUrl = configuration.BaseUrl
            };
        }

        var client = new Client(
            vertexAI: platformConfiguration.UseVertexAI,
            apiKey: apiKey,
            httpOptions: httpOptions);
        var model = request.Role.ModelName ?? "gemini-2.5-flash";
        var chatClient = client.AsIChatClient(model);
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

        var agent = chatClient.AsAIAgent(
            agentOptions,
            serviceProvider.GetRequiredService<ILoggerFactory>(),
            serviceProvider);
        return agent.AsTaskAgent().AsFeatureTaskAgent(serviceProvider, agent);
    }
}
