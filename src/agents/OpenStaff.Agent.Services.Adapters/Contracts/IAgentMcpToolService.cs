using Microsoft.Extensions.AI;
using OpenStaff.Agent.Services;

namespace OpenStaff.Agent.Services.Adapters;

/// <summary>
/// zh-CN: 为默认应用运行时适配层暴露基于 MCP 的工具发现与能力授予接口。
/// en: Exposes MCP-backed tool discovery and capability granting for the default application runtime adapters.
/// </summary>
public interface IAgentMcpToolService
{
    /// <summary>
    /// zh-CN: 按当前执行上下文加载已启用的 MCP 工具。
    /// en: Loads the enabled MCP tools for the current execution context.
    /// </summary>
    Task<List<AITool>> LoadEnabledToolsAsync(AgentMcpToolLoadContext context, CancellationToken ct);

    /// <summary>
    /// zh-CN: 确保项目智能体具备执行当前任务所需的工具能力。
    /// en: Ensures a project agent has the tool capabilities required for the current task.
    /// </summary>
    Task<AgentMcpCapabilityGrantResult> EnsureToolsAllowedAsync(
        Guid projectAgentId,
        IReadOnlyCollection<string> requiredTools,
        CancellationToken ct);
}

/// <summary>
/// zh-CN: 描述一次 MCP 运行时能力申请的处理结果。
/// en: Describes the result of attempting to satisfy a runtime capability request from MCP-backed tools.
/// </summary>
/// <param name="SatisfiedTools">
/// zh-CN: 已满足或已授予的工具集合。
/// en: The tools that were already satisfied or successfully granted.
/// </param>
/// <param name="MissingTools">
/// zh-CN: 仍然缺失的工具集合。
/// en: The tools that are still missing.
/// </param>
/// <param name="Changed">
/// zh-CN: 本次处理是否实际修改了能力授权状态。
/// en: Indicates whether capability state changed during the operation.
/// </param>
public sealed record AgentMcpCapabilityGrantResult(
    IReadOnlyList<string> SatisfiedTools,
    IReadOnlyList<string> MissingTools,
    bool Changed);

/// <summary>
/// zh-CN: 描述一次消息执行应从哪一层绑定作用域读取 MCP 工具。
/// en: Describes which binding scope a message execution should use when loading MCP tools.
/// </summary>
/// <param name="Scene">
/// zh-CN: 当前消息所属的运行场景。
/// en: The runtime scene of the current message.
/// </param>
/// <param name="ProjectAgentRoleId">
/// zh-CN: 项目真实运行时使用的项目成员标识。
/// en: Project-agent identifier used for real project execution.
/// </param>
/// <param name="AgentRoleId">
/// zh-CN: 测试场景下使用的角色标识。
/// en: Agent-role identifier used for test-chat execution.
/// </param>
public sealed record AgentMcpToolLoadContext(
    MessageScene Scene,
    Guid? ProjectAgentRoleId,
    Guid? AgentRoleId,
    Guid? SessionId = null,
    string? DispatchSource = null);
