using Anthropic;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenStaff.Plugin.Services;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;
using System.ClientModel.Primitives;

namespace OpenStaff.Plugin;

internal class GitHubCopilotChatClientFactory : DefaultChatClientFactoryBase
{
   protected CopilotToken? CurrentToken { get; private set; }
    protected override bool GetUrlSkipVersionLabel() => true;

    public GitHubCopilotChatClientFactory(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }
    override protected async Task<Provider.Models.ModelInfo> GetModelAsync(string modelId)
    {
        var protocol = CreateInstance<GitHubCopilotProtocol>();
        var currentProviderDetail = GetRequiredService<ICurrentProviderDetail>();
        if (currentProviderDetail.Current is { })
        {
            var configurationResult = await protocol.LoadConfigurationAsync(currentProviderDetail.Current.AccountId);
            protocol.Initialize(configurationResult.Configuration);
        }
        var models = await protocol.ModelsAsync();
        return models.FirstOrDefault(m => m.ModelSlug == modelId) ?? throw new InvalidOperationException($"Model with id {modelId} not found.");
    }
    protected override async Task<IChatClient> CreateChatClientAsync(ModelProtocolType modelProtocol, ChatClientCreateRequest request)
    {
        var environment = await LoadConfigurationAsync<GitHubCopilotProtocolEnv>(request.AccountId);
        if (environment == null)
            throw new InvalidOperationException($"API key environment configuration for account '{request.AccountId}' could not be loaded.");

        //var baseUrl = environment.BaseUrl;
        CurrentToken = await GetRequiredService<CopilotTokenService>().GetTokenAsync(environment.OAuthToken);
        var apiKey = CurrentToken.Token;
        var baseUrl = CurrentToken.ChatCompletionsEndpoint;
        
        return modelProtocol switch
        {
            ModelProtocolType.OpenAIChatCompletions => CreateOpenAICompatibleChatClient(request.ModelId, apiKey, baseUrl),
            ModelProtocolType.OpenAIResponse => CreateOpenAIResponseChatClient(request.ModelId, apiKey, baseUrl),
            ModelProtocolType.AnthropicMessages => CreateAnthropicMessagesChatClient(request.ModelId, apiKey, baseUrl),
            ModelProtocolType.GoogleGenerateContent => CreateGoogleGenerateContentChatClient(request.ModelId, apiKey, baseUrl),
            _ => throw new NotSupportedException($"Model protocol {modelProtocol} is not supported by {GetType().Name}.")
        };
    }

    protected override void ConfigureOpenAIClientOptions(OpenAIClientOptions options)
    {
        options.AddPolicy(new GitHubCopilotPolicy(), PipelinePosition.PerCall);
    }


    protected override IChatClient CreateAnthropicMessagesChatClient(string modelId, string apiKey, string? baseUrl = null)
    {
        var clientOptions = new Anthropic.Core.ClientOptions()
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl!,
            AuthToken=apiKey,
        };
        ConfigureHttpClient(clientOptions.HttpClient);
        var client = new AnthropicClient(clientOptions);
        return client.AsIChatClient(modelId);
    }
    protected override void ConfigureHttpClient(HttpClient httpClient)
    { 
        GitHubCopilotHttpClientHelper.ConfigureHttpClient(httpClient);
        if (CurrentToken is { })
        {
            //httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CurrentToken.Token);
        }
    }
}

internal class GitHubCopilotPolicy : PipelinePolicy
{
    protected static Dictionary<string, string> Headers { get; } = new() {
        {"Editor-Version","vscode/1.104.1" },
        {"Editor-Plugin-Version", "copilot-chat/0.27.0" },
        { "Copilot-Integration-Id","vscode-chat"},
        { "User-Agent","GitHubCopilotChat/1.0.102"}
    };

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        foreach (var header in Headers)
            message.Request.Headers.Set(header.Key, header.Value);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        foreach (var header in Headers)
            message.Request.Headers.Set(header.Key, header.Value);
        return ProcessNextAsync(message, pipeline, currentIndex);
    }
}