using System.ClientModel;
using Anthropic;
using Google.GenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenStaff.Core.Models;

namespace OpenStaff.Agents;

/// <summary>
/// AI Agent 工厂 — 根据供应商类型创建对应的 AIAgent 实例
/// 参考 microsoft/agent-framework AgentProviders 示例
/// </summary>
public class AIAgentFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public AIAgentFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 创建 AIAgent — 根据供应商类型选择正确的 SDK
    /// </summary>
    /// <param name="provider">供应商配置</param>
    /// <param name="apiKey">已解密的 API Key</param>
    /// <param name="modelName">模型名称（覆盖供应商默认值）</param>
    /// <param name="instructions">系统提示词</param>
    /// <param name="agentName">代理体名称</param>
    public AIAgent CreateAgent(
        ModelProvider provider,
        string apiKey,
        string? modelName = null,
        string? instructions = null,
        string? agentName = null)
    {
        var model = modelName ?? provider.DefaultModel ?? "gpt-4o";
        var logger = _loggerFactory.CreateLogger<AIAgentFactory>();

        logger.LogInformation("Creating AIAgent for provider {Provider} ({Type}), model={Model}",
            provider.Name, provider.ProviderType, model);

        return provider.ProviderType switch
        {
            ProviderTypes.OpenAI => CreateOpenAIAgent(apiKey, model, provider.BaseUrl, instructions, agentName),
            ProviderTypes.GitHubCopilot => CreateGitHubCopilotAgent(apiKey, model, provider.BaseUrl, instructions, agentName),
            ProviderTypes.Anthropic => CreateAnthropicAgent(apiKey, model, instructions, agentName),
            ProviderTypes.Google => CreateGoogleAgent(apiKey, model, instructions, agentName),
            ProviderTypes.GenericOpenAI => CreateOpenAIAgent(apiKey, model, provider.BaseUrl, instructions, agentName),
            ProviderTypes.AzureOpenAI => CreateOpenAIAgent(apiKey, model, provider.BaseUrl, instructions, agentName),
            _ => throw new NotSupportedException($"不支持的供应商类型: {provider.ProviderType}")
        };
    }

    /// <summary>
    /// OpenAI — 参考 Agent_With_OpenAIChatCompletion
    /// new OpenAIClient(apiKey).GetChatClient(model).AsAIAgent(instructions, name)
    /// </summary>
    private static AIAgent CreateOpenAIAgent(string apiKey, string model, string? baseUrl, string? instructions, string? name)
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

        return client
            .GetChatClient(model)
            .AsAIAgent(instructions: instructions, name: name);
    }

    /// <summary>
    /// GitHub Copilot — 使用 OpenAI SDK 连接 Copilot API 端点
    /// 使用 Device Auth 获取的 token 作为 Bearer token
    /// </summary>
    private static AIAgent CreateGitHubCopilotAgent(string apiKey, string model, string? baseUrl, string? instructions, string? name)
    {
        var endpoint = baseUrl ?? "https://api.githubcopilot.com";
        var credential = new ApiKeyCredential(apiKey);
        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        var client = new OpenAIClient(credential, options);

        return client
            .GetChatClient(model)
            .AsAIAgent(instructions: instructions, name: name);
    }

    /// <summary>
    /// Anthropic — 参考 Agent_With_Anthropic
    /// new AnthropicClient() { ApiKey = key }.AsAIAgent(model, instructions, name)
    /// </summary>
    private static AIAgent CreateAnthropicAgent(string apiKey, string model, string? instructions, string? name)
    {
        var client = new AnthropicClient { ApiKey = apiKey };
        return client.AsAIAgent(model: model, instructions: instructions, name: name);
    }

    /// <summary>
    /// Google Gemini — 参考 Agent_With_GoogleGemini
    /// new Client(apiKey: key).AsIChatClient(model) → ChatClientAgent
    /// </summary>
    private static AIAgent CreateGoogleAgent(string apiKey, string model, string? instructions, string? name)
    {
        var client = new Client(vertexAI: false, apiKey: apiKey);
        var chatClient = client.AsIChatClient(model);
        return new ChatClientAgent(chatClient, name: name, instructions: instructions);
    }
}
