using Anthropic;
using Google.GenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenStaff.Core.Models;
using System.ClientModel;

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
            ProviderTypes.GitHubCopilot => CreateOpenAIChatClient(apiKey, model, provider.BaseUrl ?? "https://api.individual.githubcopilot.com"),
            ProviderTypes.GenericOpenAI => CreateOpenAIChatClient(apiKey, model, provider.BaseUrl),
            ProviderTypes.AzureOpenAI => CreateOpenAIChatClient(apiKey, model, provider.BaseUrl),
            ProviderTypes.Anthropic => CreateAnthropicChatClient(apiKey, model),
            ProviderTypes.Google => CreateGoogleChatClient(apiKey, model),
            _ => throw new NotSupportedException($"不支持的供应商类型: {provider.ProviderType}")
        };
    }

    /// <summary>
    /// OpenAI 兼容 — OpenAI / Copilot / GenericOpenAI / AzureOpenAI
    /// ChatClient.AsIChatClient()
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
}
