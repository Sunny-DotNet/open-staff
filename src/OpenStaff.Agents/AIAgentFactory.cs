using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace OpenStaff.Agents;

/// <summary>
/// AI Agent 工厂 — 从 IChatClient 创建 AIAgent 实例
/// </summary>
public class AIAgentFactory
{
    private readonly ChatClientFactory _chatClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public AIAgentFactory(ChatClientFactory chatClientFactory, ILoggerFactory loggerFactory)
    {
        _chatClientFactory = chatClientFactory;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 创建 AIAgent
    /// </summary>
    /// <param name="protocolType">协议类型 (openai, anthropic, google, etc.)</param>
    /// <param name="apiKey">已解密的 API Key</param>
    /// <param name="model">模型名称</param>
    /// <param name="baseUrl">API 端点（可选覆盖）</param>
    /// <param name="instructions">系统提示词</param>
    /// <param name="agentName">代理体名称</param>
    /// <param name="tools">AITool 列表</param>
    public AIAgent CreateAgent(
        string protocolType,
        string apiKey,
        string model,
        string? baseUrl = null,
        string? instructions = null,
        string? agentName = null,
        IList<AITool>? tools = null)
    {
        var chatClient = _chatClientFactory.Create(protocolType, apiKey, model, baseUrl);

        _loggerFactory.CreateLogger<AIAgentFactory>()
            .LogInformation("Creating AIAgent '{Name}' with {ToolCount} tools",
                agentName, tools?.Count ?? 0);

        return new ChatClientAgent(
            chatClient,
            name: agentName,
            instructions: instructions,
            tools: tools,
            loggerFactory: _loggerFactory);
    }
}
