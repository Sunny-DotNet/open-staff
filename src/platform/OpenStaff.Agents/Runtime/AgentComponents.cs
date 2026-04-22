using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace OpenStaff.Agent;

/// <summary>
/// zh-CN: 聚合一次智能体准备阶段产出的可复用组件。
/// en: Aggregates the reusable components produced during agent preparation.
/// </summary>
/// <param name="Agent">
/// zh-CN: 已创建完成的智能体实例。
/// en: The constructed agent instance.
/// </param>
/// <param name="Name">
/// zh-CN: 用于日志或界面显示的名称。
/// en: The name used for logging or display.
/// </param>
/// <param name="Instructions">
/// zh-CN: 最终生成的系统指令文本。
/// en: The final system instructions passed to the agent.
/// </param>
/// <param name="Tools">
/// zh-CN: 绑定到该智能体的工具集合。
/// en: The tools bound to the agent.
/// </param>
public record AgentComponents(
    AIAgent Agent,
    string Name,
    string? Instructions,
    IList<AITool>? Tools);
