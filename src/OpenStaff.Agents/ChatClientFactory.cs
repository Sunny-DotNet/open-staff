using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace OpenStaff.Agents;

/// <summary>
/// IChatClient 工厂 — 仅处理 OpenAI 兼容协议（openai, github-copilot, NewApi 等）
/// Vendor 智能体（Anthropic, Google）通过各自的 Vendor 项目直接创建
/// </summary>
public class ChatClientFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public ChatClientFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 创建 IChatClient — 仅支持 OpenAI 兼容协议
    /// </summary>
    public IChatClient Create(string protocolType, string apiKey, string model, string? baseUrl = null)
    {
        var logger = _loggerFactory.CreateLogger<ChatClientFactory>();
        logger.LogInformation("Creating IChatClient for protocol {Protocol}, model={Model}", protocolType, model);

        return protocolType switch
        {
            "openai" => CreateOpenAIChatClient(apiKey, model, baseUrl),
            "github-copilot" => CreateCopilotChatClient(apiKey, model, baseUrl),
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
