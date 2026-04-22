namespace OpenStaff.Agent.Services;

/// <summary>
/// zh-CN: 记录一条消息执行结束时的终态摘要。
/// en: Captures the terminal summary of a completed, failed, or cancelled message execution.
/// </summary>
/// <param name="MessageId">
/// zh-CN: 逻辑消息标识。
/// en: The logical message identifier.
/// </param>
/// <param name="Scene">
/// zh-CN: 运行场景。
/// en: The runtime scene.
/// </param>
/// <param name="Context">
/// zh-CN: 执行上下文。
/// en: The execution context.
/// </param>
/// <param name="Success">
/// zh-CN: 是否成功完成。
/// en: Indicates whether execution completed successfully.
/// </param>
/// <param name="Cancelled">
/// zh-CN: 是否由取消导致结束。
/// en: Indicates whether execution ended due to cancellation.
/// </param>
/// <param name="Attempts">
/// zh-CN: 实际尝试次数。
/// en: The number of attempts that were made.
/// </param>
/// <param name="AgentRole">
/// zh-CN: 执行时的角色类型。
/// en: The role type used during execution.
/// </param>
/// <param name="Model">
/// zh-CN: 执行时的模型名称。
/// en: The model name used during execution.
/// </param>
/// <param name="Content">
/// zh-CN: 累积得到的正文内容。
/// en: The accumulated response content.
/// </param>
/// <param name="Thinking">
/// zh-CN: 累积得到的思考内容。
/// en: The accumulated reasoning content.
/// </param>
/// <param name="Usage">
/// zh-CN: 最后的令牌用量快照。
/// en: The final token-usage snapshot.
/// </param>
/// <param name="Timing">
/// zh-CN: 最后的耗时快照。
/// en: The final timing snapshot.
/// </param>
/// <param name="ToolCalls">
/// zh-CN: 观察到的工具调用终态列表。
/// en: The terminal snapshots of observed tool calls.
/// </param>
/// <param name="Error">
/// zh-CN: 失败或取消时的错误信息。
/// en: The error text when execution fails or is cancelled.
/// </param>
public sealed record MessageExecutionSummary(
    Guid MessageId,
    MessageScene Scene,
    MessageContext Context,
    bool Success,
    bool Cancelled,
    int Attempts,
    string? AgentRole,
    string? Model,
    string Content,
    string Thinking,
    MessageUsageSnapshot? Usage,
    MessageTimingSnapshot? Timing,
    IReadOnlyList<ToolInvocationSnapshot> ToolCalls,
    string? Error);

/// <summary>
/// zh-CN: 记录提供程序报告的最新令牌用量。
/// en: Captures the latest token usage reported by the provider.
/// </summary>
/// <param name="InputTokens">
/// zh-CN: 输入令牌数。
/// en: The input token count.
/// </param>
/// <param name="OutputTokens">
/// zh-CN: 输出令牌数。
/// en: The output token count.
/// </param>
/// <param name="TotalTokens">
/// zh-CN: 总令牌数。
/// en: The total token count.
/// </param>
public sealed record MessageUsageSnapshot(
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens);

/// <summary>
/// zh-CN: 记录消息执行的耗时指标。
/// en: Captures elapsed runtime measurements for a message execution.
/// </summary>
/// <param name="TotalMs">
/// zh-CN: 总耗时（毫秒）。
/// en: The total elapsed time in milliseconds.
/// </param>
/// <param name="FirstTokenMs">
/// zh-CN: 首个可见输出出现时的耗时（毫秒）。
/// en: The elapsed time in milliseconds until the first visible token arrives.
/// </param>
public sealed record MessageTimingSnapshot(
    long TotalMs,
    long? FirstTokenMs);

/// <summary>
/// zh-CN: 记录一次工具调用在消息结束时的终态快照。
/// en: Captures the terminal snapshot of a tool invocation observed during message execution.
/// </summary>
/// <param name="CallId">
/// zh-CN: 工具调用标识。
/// en: The tool-call identifier.
/// </param>
/// <param name="Name">
/// zh-CN: 工具名称。
/// en: The tool name.
/// </param>
/// <param name="Arguments">
/// zh-CN: 序列化后的调用参数。
/// en: The serialized invocation arguments.
/// </param>
/// <param name="Result">
/// zh-CN: 工具返回值。
/// en: The tool result text.
/// </param>
/// <param name="Error">
/// zh-CN: 工具错误信息。
/// en: The tool error text.
/// </param>
/// <param name="Status">
/// zh-CN: 工具调用最终状态。
/// en: The final tool-invocation status.
/// </param>
public sealed record ToolInvocationSnapshot(
    string CallId,
    string Name,
    string? Arguments,
    string? Result,
    string? Error,
    ToolInvocationStatus Status);
