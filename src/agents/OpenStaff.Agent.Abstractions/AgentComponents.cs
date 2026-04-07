using Microsoft.Extensions.AI;

namespace OpenStaff.Agent;

/// <summary>
/// 智能体组件：ChatClient + 指令 + 工具，支持流式调用等场景复用
/// </summary>
public record AgentComponents(
    IChatClient ChatClient,
    string Name,
    string? Instructions,
    IList<AITool>? Tools);
