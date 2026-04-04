using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Models;

namespace OpenStaff.Agents;

/// <summary>
/// AI Agent 工厂 — 从 IChatClient 创建 AIAgent 实例
/// 统一入口：IChatClient + 提示词 + 工具 → ChatClientAgent (AIAgent)
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
    /// 创建 AIAgent — 统一走 IChatClient → ChatClientAgent 路径
    /// </summary>
    /// <param name="provider">供应商配置</param>
    /// <param name="apiKey">已解密的 API Key</param>
    /// <param name="modelName">模型名称（覆盖供应商默认值）</param>
    /// <param name="instructions">系统提示词</param>
    /// <param name="agentName">代理体名称</param>
    /// <param name="tools">AITool 列表（已从 IAgentTool 桥接转换）</param>
    public AIAgent CreateAgent(
        ModelProvider provider,
        string apiKey,
        string? modelName = null,
        string? instructions = null,
        string? agentName = null,
        IList<AITool>? tools = null)
    {
        var chatClient = _chatClientFactory.Create(provider, apiKey, modelName);

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
