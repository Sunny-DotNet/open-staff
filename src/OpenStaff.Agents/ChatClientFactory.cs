using Anthropic;
using Google.GenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace OpenStaff.Agents;

/// <summary>
/// IChatClient 工厂 — 根据协议类型创建统一的 IChatClient 实例
/// </summary>
public class ChatClientFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public ChatClientFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 创建 IChatClient — 根据协议类型选择正确的 SDK
    /// </summary>
    public IChatClient Create(string protocolType, string apiKey, string model, string? baseUrl = null)
    {
        var logger = _loggerFactory.CreateLogger<ChatClientFactory>();
        logger.LogInformation("Creating IChatClient for protocol {Protocol}, model={Model}", protocolType, model);

        return protocolType switch
        {
            "openai" => CreateOpenAIChatClient(apiKey, model, baseUrl),
            "github-copilot" => CreateCopilotChatClient(apiKey, model, baseUrl),
            "anthropic" => CreateAnthropicChatClient(apiKey, model),
            "google" => CreateGoogleChatClient(apiKey, model),
            // NewApi 和其他 OpenAI 兼容协议
            _ => CreateOpenAIChatClient(apiKey, model, baseUrl)
        };
    }

    private static IChatClient CreateOpenAIChatClient(string apiKey, string model, string? baseUrl)
    {
        var credential = new ApiKeyCredential(apiKey);
        OpenAIClientOptions? options = null;
        if (!string.IsNullOrEmpty(baseUrl))
        {
            // OpenAI SDK v2 使用 {endpoint}/chat/completions 路径，
            // 对于 OpenAI 兼容 API（NewAPI/OneAPI 等），需要确保 endpoint 包含 /v1
            var uri = baseUrl.TrimEnd('/');
            if (!uri.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                uri += "/v1";
            options = new OpenAIClientOptions { Endpoint = new Uri(uri) };
        }

        var client = options != null
            ? new OpenAIClient(credential, options)
            : new OpenAIClient(credential);

        return client.GetChatClient(model).AsIChatClient();
    }

    private static IChatClient CreateCopilotChatClient(string apiKey, string model, string? baseUrl)
    {
        var endpoint = baseUrl ?? "https://api.individual.githubcopilot.com";
        var credential = new ApiKeyCredential(apiKey);
        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        options.AddPolicy(new CopilotHeaderPolicy(), PipelinePosition.PerCall);

        var client = new OpenAIClient(credential, options);
        return client.GetChatClient(model).AsIChatClient();
    }

    private static IChatClient CreateAnthropicChatClient(string apiKey, string model)
    {
        var client = new AnthropicClient { ApiKey = apiKey };
        return client.AsIChatClient(model);
    }

    private static IChatClient CreateGoogleChatClient(string apiKey, string model)
    {
        var client = new Client(vertexAI: false, apiKey: apiKey);
        return client.AsIChatClient(model);
    }

    private sealed class CopilotHeaderPolicy : PipelinePolicy
    {
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            AddCopilotHeaders(message);
            ProcessNext(message, pipeline, currentIndex);
        }

        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            AddCopilotHeaders(message);
            await ProcessNextAsync(message, pipeline, currentIndex);
        }

        private static void AddCopilotHeaders(PipelineMessage message)
        {
            var headers = message.Request.Headers;
            headers.Set("Editor-Version", "vscode/1.96.2");
            headers.Set("X-Github-Api-Version", "2025-04-01");
            headers.Set("User-Agent", "GitHubCopilotChat/1.0.102");
        }
    }
}
