using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenStaff.Agents;

namespace OpenStaff.Agent.Services;

/// <summary>
/// zh-CN: 为单条逻辑消息解析执行所需的智能体、历史消息与运行选项。
/// en: Resolves the agent, message history, and runtime options required to execute a logical message.
/// </summary>
public interface IAgentRunFactory
{
    /// <summary>
    /// zh-CN: 为指定消息准备提供程序相关的执行上下文。
    /// en: Prepares the provider-specific execution context for the supplied message.
    /// </summary>
    Task<PreparedAgentRun> PrepareAsync(
        CreateMessageRequest request,
        Guid messageId,
        CancellationToken cancellationToken);
}

/// <summary>
/// zh-CN: 承载一次任务执行已经准备好的智能体，以及为上下文绑定型 task-agent 保留下来的历史消息和运行元数据。
/// en: Carries the prepared task-capable agent together with the restored messages and execution metadata retained for contextual task-agent binding.
/// </summary>
/// <param name="Agent">
/// zh-CN: 已准备完成的智能体实例。
/// en: The prepared agent instance.
/// </param>
/// <param name="Messages">
/// zh-CN: 已恢复的对话消息序列；对于上下文绑定型智能体，这些消息已在准备阶段绑定到返回的 <paramref name="Agent" />。
/// en: The restored conversation messages; for contextual task agents these messages have already been bound to the returned <paramref name="Agent" /> during preparation.
/// </param>
/// <param name="Session">
/// zh-CN: 可选的提供程序会话对象，便于调试和测试观察准备结果。
/// en: The optional provider-specific session object retained for diagnostics and tests.
/// </param>
/// <param name="RunOptions">
/// zh-CN: 本次运行的附加选项，主要供调试和测试观察。
/// en: Additional options for the current run, primarily retained for diagnostics and tests.
/// </param>
/// <param name="AgentRole">
/// zh-CN: 本次执行选定的角色类型。
/// en: The role type selected for the current execution.
/// </param>
/// <param name="Model">
/// zh-CN: 本次执行将使用的模型名称。
/// en: The model name used for the current execution.
/// </param>
public sealed record PreparedAgentRun(
    IStaffAgent Agent,
    IReadOnlyList<ChatMessage> Messages,
    AgentSession? Session = null,
    AgentRunOptions? RunOptions = null,
    IAsyncDisposable? ExecutionLease = null,
    string? AgentRole = null,
    string? Model = null);
