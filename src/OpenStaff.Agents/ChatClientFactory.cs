using Anthropic;
using Google.GenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenStaff.Core.Models;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace OpenStaff.Agents;

/// <summary>
/// IChatClient 工厂 — 根据供应商类型创建统一的 IChatClient 实例
/// 所有供应商最终都转为 IChatClient，供 AIAgentFactory 统一包装为 AIAgent
/// </summary>
public class ChatClientFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public ChatClientFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 创建 IChatClient — 根据供应商类型选择正确的 SDK
    /// </summary>
    public IChatClient Create(ModelProvider provider, string apiKey, string? modelName = null)
    {
        var model = modelName ?? provider.DefaultModel ?? "gpt-4o";
        var logger = _loggerFactory.CreateLogger<ChatClientFactory>();

        logger.LogInformation("Creating IChatClient for provider {Provider} ({Type}), model={Model}",
            provider.Name, provider.ProviderType, model);

        return provider.ProviderType switch
        {
            ProviderTypes.OpenAI => CreateOpenAIChatClient(apiKey, model, provider.BaseUrl),
            ProviderTypes.GitHubCopilot => CreateCopilotChatClient(apiKey, model, provider.BaseUrl),
            ProviderTypes.GenericOpenAI => CreateOpenAIChatClient(apiKey, model, provider.BaseUrl),
            ProviderTypes.AzureOpenAI => CreateOpenAIChatClient(apiKey, model, provider.BaseUrl),
            ProviderTypes.Anthropic => CreateAnthropicChatClient(apiKey, model),
            ProviderTypes.Google => CreateGoogleChatClient(apiKey, model),
            _ => throw new NotSupportedException($"不支持的供应商类型: {provider.ProviderType}")
        };
    }

    /// <summary>
    /// OpenAI 兼容 — OpenAI / GenericOpenAI / AzureOpenAI
    /// </summary>
    private static IChatClient CreateOpenAIChatClient(string apiKey, string model, string? baseUrl)
    {
        var credential = new ApiKeyCredential(apiKey);

        OpenAIClientOptions? options = null;
        if (!string.IsNullOrEmpty(baseUrl))
        {
            options = new OpenAIClientOptions { Endpoint = new Uri(baseUrl) };
        }

        var client = options != null
            ? new OpenAIClient(credential, options)
            : new OpenAIClient(credential);

        return client.GetChatClient(model).AsIChatClient();
    }

    /// <summary>
    /// GitHub Copilot — OpenAI SDK + 自定义 endpoint + 必要的请求头
    /// Copilot API 要求 Editor-Version、Editor-Plugin-Version、Copilot-Integration-Id 等头
    /// </summary>
    private static IChatClient CreateCopilotChatClient(string apiKey, string model, string? baseUrl)
    {
        var endpoint = baseUrl ?? "https://api.individual.githubcopilot.com";
        var credential = new ApiKeyCredential(apiKey);
        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };

        // 注入 Copilot 必需的请求头
        options.AddPolicy(new CopilotHeaderPolicy(), PipelinePosition.PerCall);

        var client = new OpenAIClient(credential, options);
        return client.GetChatClient(model).AsIChatClient();
    }

    /// <summary>
    /// Anthropic — AnthropicClient.AsIChatClient(model)
    /// </summary>
    private static IChatClient CreateAnthropicChatClient(string apiKey, string model)
    {
        var client = new AnthropicClient { ApiKey = apiKey };
        return client.AsIChatClient(model);
    }

    /// <summary>
    /// Google Gemini — Client.AsIChatClient(model)
    /// </summary>
    private static IChatClient CreateGoogleChatClient(string apiKey, string model)
    {
        var client = new Client(vertexAI: false, apiKey: apiKey);
        return client.AsIChatClient(model);
    }

    /// <summary>
    /// 为 Copilot API 请求注入必需的 Editor 头
    /// </summary>
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
            //headers.Set("Editor-Plugin-Version", "copilot-openstaff/1.0.0");
            //headers.Set("Copilot-Integration-Id", "openstaff");
        }
    }
}
