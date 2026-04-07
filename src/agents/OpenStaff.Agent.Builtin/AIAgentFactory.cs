using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace OpenStaff.Agent.Builtin;

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
