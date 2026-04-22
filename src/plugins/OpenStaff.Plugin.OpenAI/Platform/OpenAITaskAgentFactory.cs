using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenHub.Agents;
using OpenAI;
using OpenStaff.Agents;
using OpenStaff.Agent;
using OpenStaff.Core.Agents;
using System.ClientModel;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Plugin.OpenAI;

internal sealed class OpenAITaskAgentFactory(
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory) : ITaskAgentFactory
{
    public async Task<OpenHub.Agents.ITaskAgent> CreateAsync(
        TaskAgentCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawContext = request.RawContext as AgentContext;
        var accountId = request.Role.ModelProviderId
            ?? throw new InvalidOperationException("OpenAI runtime requires a bound provider account.");
        dynamic protocol = ActivatorUtilities.CreateInstance<OpenAIProtocol>(serviceProvider);
        var configurationResult = await protocol.LoadConfigurationAsync(accountId.ToString(), cancellationToken);
        var configuration = (OpenAIProtocolEnv?)configurationResult.Configuration
            ?? throw new InvalidOperationException("请先在供应商管理中配置 OpenAI API Key");

        var config = AgentConfig.FromJson(request.Role.Config);
        var model = config.Get("model") ?? request.Role.ModelName ?? "gpt-4o";

        var credential = new ApiKeyCredential(configuration.ApiKey);
        var options = new OpenAIClientOptions();
        if (!string.IsNullOrEmpty(configuration.BaseUrl))
            options.Endpoint = new Uri(configuration.BaseUrl);

        var client = new OpenAIClient(credential, options);
        IChatClient chatClient = client.GetChatClient(model).AsIChatClient();

        // OpenAI 平台的 Skill 同样不是 Tool，而是上下文提供器。
        var contextProviders = rawContext == null
            ? []
            : AgentSkillContextProviderFactory.CreateProviders(rawContext, serviceProvider, loggerFactory);
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

        var agent = chatClient.AsAIAgent(agentOptions, loggerFactory, serviceProvider);
        return agent.AsTaskAgent().AsFeatureTaskAgent(serviceProvider, agent);
    }
}

