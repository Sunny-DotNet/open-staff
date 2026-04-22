namespace OpenStaff.Agent.Services;

/// <summary>
/// zh-CN: 描述智能体消息执行过程中发出的运行时事件类型。
/// en: Describes the kinds of runtime events emitted while an agent message executes.
/// </summary>
public enum AgentMessageEventType
{
    /// <summary>
    /// zh-CN: 运行时已接受消息。
    /// en: The runtime accepted the message.
    /// </summary>
    Accepted,

    /// <summary>
    /// zh-CN: 运行正式开始。
    /// en: Execution started.
    /// </summary>
    Started,

    /// <summary>
    /// zh-CN: 收到思考内容增量。
    /// en: A reasoning chunk was received.
    /// </summary>
    ThinkingChunk,

    /// <summary>
    /// zh-CN: 收到正文内容增量。
    /// en: A visible content chunk was received.
    /// </summary>
    ContentChunk,

    /// <summary>
    /// zh-CN: 触发了工具调用。
    /// en: A tool invocation was issued.
    /// </summary>
    ToolCall,

    /// <summary>
    /// zh-CN: 收到了工具结果。
    /// en: A tool result was received.
    /// </summary>
    ToolResult,

    /// <summary>
    /// zh-CN: 工具调用发生错误。
    /// en: A tool invocation failed.
    /// </summary>
    ToolError,

    /// <summary>
    /// zh-CN: 用量信息已更新。
    /// en: Usage information was updated.
    /// </summary>
    UsageUpdated,

    /// <summary>
    /// zh-CN: 运行时已安排重试。
    /// en: The runtime scheduled a retry.
    /// </summary>
    RetryScheduled,

    /// <summary>
    /// zh-CN: 执行成功完成。
    /// en: Execution completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// zh-CN: 执行失败结束。
    /// en: Execution ended with an error.
    /// </summary>
    Error,

    /// <summary>
    /// zh-CN: 执行被取消。
    /// en: Execution was cancelled.
    /// </summary>
    Cancelled
}

/// <summary>
/// zh-CN: 描述单次工具调用在响应流中的生命周期状态。
/// en: Describes the lifecycle state of a single tool invocation inside an agent response.
/// </summary>
public enum ToolInvocationStatus
{
    /// <summary>
    /// zh-CN: 工具调用中。
    /// en: The tool is being invoked.
    /// </summary>
    Calling,

    /// <summary>
    /// zh-CN: 工具调用成功完成。
    /// en: The tool invocation completed successfully.
    /// </summary>
    Done,

    /// <summary>
    /// zh-CN: 工具调用失败。
    /// en: The tool invocation failed.
    /// </summary>
    Error
}

/// <summary>
/// zh-CN: 表示智能体处理消息时产生的一条可观察运行时事件。
/// en: Represents one observable runtime event produced while an agent handles a message.
/// </summary>
public sealed record AgentMessageEvent
{
    /// <summary>
    /// zh-CN: 获取产生该事件的逻辑消息标识。
    /// en: Gets the logical message identifier that owns the event.
    /// </summary>
    public required Guid MessageId { get; init; }

    /// <summary>
    /// zh-CN: 获取事件类型。
    /// en: Gets the event kind.
    /// </summary>
    public required AgentMessageEventType EventType { get; init; }

    /// <summary>
    /// zh-CN: 获取产生该事件的场景。
    /// en: Gets the scene that produced the event.
    /// </summary>
    public required MessageScene Scene { get; init; }

    /// <summary>
    /// zh-CN: 获取产生该事件的执行上下文。
    /// en: Gets the execution context that produced the event.
    /// </summary>
    public required MessageContext Context { get; init; }

    /// <summary>
    /// zh-CN: 获取事件生成时的实际时间。
    /// en: Gets the wall-clock time when the event was produced.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// zh-CN: 获取该逻辑消息当前的尝试次数。
    /// en: Gets the current attempt number for the logical message.
    /// </summary>
    public int Attempt { get; init; }

    /// <summary>
    /// zh-CN: 获取与事件关联的正文片段或最终回复文本。
    /// en: Gets the content chunk or final response text associated with the event.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// zh-CN: 获取与事件关联的思考片段。
    /// en: Gets the reasoning chunk associated with the event.
    /// </summary>
    public string? ThoughtText { get; init; }

    /// <summary>
    /// zh-CN: 获取与事件关联的错误文本。
    /// en: Gets the error text associated with the event, when present.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// zh-CN: 获取提供程序暴露的消息标识（如果有）。
    /// en: Gets the provider-specific message identifier when the provider exposes one.
    /// </summary>
    public string? ProviderMessageId { get; init; }

    /// <summary>
    /// zh-CN: 获取本次执行选定的逻辑角色。
    /// en: Gets the logical agent role selected for execution.
    /// </summary>
    public string? AgentRole { get; init; }

    /// <summary>
    /// zh-CN: 获取本次尝试使用的模型名称。
    /// en: Gets the model name used for the attempt.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// zh-CN: 获取与事件关联的工具名称。
    /// en: Gets the tool name associated with the event.
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// zh-CN: 获取与事件关联的工具调用标识。
    /// en: Gets the tool call identifier associated with the event.
    /// </summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// zh-CN: 获取事件记录的序列化工具参数。
    /// en: Gets the serialized tool arguments captured for the event.
    /// </summary>
    public string? ToolArguments { get; init; }

    /// <summary>
    /// zh-CN: 获取事件记录的工具结果文本。
    /// en: Gets the tool result text captured for the event.
    /// </summary>
    public string? ToolResult { get; init; }

    /// <summary>
    /// zh-CN: 获取事件发生时可用的最新用量快照。
    /// en: Gets the latest usage snapshot known at the time of the event.
    /// </summary>
    public MessageUsageSnapshot? Usage { get; init; }

    /// <summary>
    /// zh-CN: 获取事件发生时可用的最新耗时快照。
    /// en: Gets the latest timing snapshot known at the time of the event.
    /// </summary>
    public MessageTimingSnapshot? Timing { get; init; }

    /// <summary>
    /// zh-CN: 获取该事件是否为逻辑消息的终态事件。
    /// en: Gets a value indicating whether the event is terminal for the logical message.
    /// </summary>
    public bool IsTerminal { get; init; }
}
