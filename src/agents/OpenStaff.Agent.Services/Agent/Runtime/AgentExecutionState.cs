using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenHub.Agents.Models;

namespace OpenStaff.Agent.Services;

/// <summary>
/// zh-CN: 将提供程序的流式更新聚合为运行时事件和终态摘要。
/// en: Aggregates streaming provider updates into runtime events and a terminal execution summary.
/// </summary>
/// <param name="messageId">zh-CN: 当前逻辑消息标识。 en: The logical message identifier for the current run.</param>
/// <param name="scene">zh-CN: 该消息所属的运行场景。 en: The runtime scene that owns the message.</param>
/// <param name="context">zh-CN: 需要原样附着到每个运行时事件上的执行上下文。 en: The execution context that is copied onto every runtime event.</param>
/// <param name="attempt">zh-CN: 当前重试轮次，从 1 开始计数。 en: The current retry attempt number, starting at 1.</param>
/// <param name="agentRole">zh-CN: 当前执行角色，用于事件与摘要中的可观测性字段。 en: The executing role used for observability fields in events and summaries.</param>
/// <param name="model">zh-CN: 本轮运行选中的模型名称。 en: The model selected for this execution attempt.</param>
internal sealed class AgentExecutionState(
    Guid messageId,
    MessageScene scene,
    MessageContext context,
    int attempt,
    string? agentRole,
    string? model)
{
    private readonly StringBuilder _content = new();
    private readonly StringBuilder _thinking = new();
    private readonly Dictionary<string, ToolInvocationState> _toolCallsById = new(StringComparer.Ordinal);
    private readonly List<ToolInvocationState> _toolCallOrder = [];

    public string? ProviderMessageId { get; set; }

    private int? InputTokens { get; set; }

    private int? OutputTokens { get; set; }

    private long? FirstTokenMs { get; set; }

    /// <summary>
    /// zh-CN: 消费一次流式更新，增量刷新内容、思考、工具调用和 usage 状态，并按观察顺序产出运行时事件。
    /// en: Consumes a streaming update, incrementally refreshes content, reasoning, tool-call, and usage state, and emits runtime events in observation order.
    /// </summary>
    public IEnumerable<AgentMessageEvent> Collect(ChatResponseUpdate update, Stopwatch stopwatch)
    {
        foreach (var reasoning in update.Contents.OfType<TextReasoningContent>())
        {
            if (string.IsNullOrWhiteSpace(reasoning.Text))
                continue;

            _thinking.Append(reasoning.Text);

            yield return CreateEvent(
                AgentMessageEventType.ThinkingChunk,
                thoughtText: reasoning.Text);
        }

        foreach (var part in update.Contents.OfType<TextContent>())
        {
            if (string.IsNullOrWhiteSpace(part.Text))
                continue;

            // zh-CN: 首 token 时间只记录第一次可见输出，便于 UI 衡量首字延迟。
            // en: Record first-token latency only for the first visible output so the UI can measure time-to-first-token.
            FirstTokenMs ??= stopwatch.ElapsedMilliseconds;
            _content.Append(part.Text);

            yield return CreateEvent(
                AgentMessageEventType.ContentChunk,
                text: part.Text);
        }

        foreach (var toolCall in update.Contents.OfType<FunctionCallContent>())
        {
            var callId = string.IsNullOrWhiteSpace(toolCall.CallId)
                ? Guid.NewGuid().ToString("N")
                : toolCall.CallId;

            // zh-CN: 某些提供程序可能在调用开始时先给出空名称或空参数，因此这里允许后续增量补齐同一个 tool state。
            // en: Some providers start a tool call with partial metadata, so keep a mutable tool state that later increments can fill in.
            var toolState = GetOrAddToolCall(
                callId,
                toolCall.Name,
                SerializeArguments(toolCall.Arguments));

            yield return CreateEvent(
                AgentMessageEventType.ToolCall,
                toolName: toolState.Name,
                toolCallId: toolState.CallId,
                toolArguments: toolState.Arguments);
        }

        foreach (var toolResult in update.Contents.OfType<FunctionResultContent>())
        {
            var callId = string.IsNullOrWhiteSpace(toolResult.CallId)
                ? Guid.NewGuid().ToString("N")
                : toolResult.CallId;

            var toolState = GetOrAddToolCall(callId, null, null);
            if (toolResult.Exception is not null)
            {
                toolState.Error = toolResult.Exception.Message;
                toolState.Status = ToolInvocationStatus.Error;

                yield return CreateEvent(
                    AgentMessageEventType.ToolError,
                    toolName: toolState.Name,
                    toolCallId: toolState.CallId,
                    error: toolState.Error);
                continue;
            }

            toolState.Result = ToDisplayText(toolResult.Result);
            toolState.Status = ToolInvocationStatus.Done;

            yield return CreateEvent(
                AgentMessageEventType.ToolResult,
                toolName: toolState.Name,
                toolCallId: toolState.CallId,
                toolResult: toolState.Result);
        }

        foreach (var usage in update.Contents.OfType<UsageContent>())
        {
            if (usage.Details == null)
                continue;

            // zh-CN: 部分提供程序会按增量回报 usage，这里累加为消息级总量快照。
            // en: Some providers emit usage incrementally, so accumulate those deltas into a message-level snapshot.
            InputTokens = (InputTokens ?? 0) + (int)(usage.Details.InputTokenCount ?? 0);
            OutputTokens = (OutputTokens ?? 0) + (int)(usage.Details.OutputTokenCount ?? 0);

            yield return CreateEvent(
                AgentMessageEventType.UsageUpdated,
                usage: BuildUsage());
        }
    }

    public AgentMessageEvent? Collect(TaskReasoningChunkEvent update)
    {
        if (string.IsNullOrWhiteSpace(update.Content))
            return null;

        _thinking.Append(update.Content);
        return CreateEvent(
            AgentMessageEventType.ThinkingChunk,
            thoughtText: update.Content);
    }

    public AgentMessageEvent? Collect(TaskContentChunkEvent update, Stopwatch stopwatch)
    {
        if (string.IsNullOrWhiteSpace(update.Content))
            return null;

        FirstTokenMs ??= stopwatch.ElapsedMilliseconds;
        _content.Append(update.Content);
        return CreateEvent(
            AgentMessageEventType.ContentChunk,
            text: update.Content);
    }

    public AgentMessageEvent Collect(TaskToolCallRequestEvent update)
    {
        var callId = string.IsNullOrWhiteSpace(update.ToolCallId)
            ? Guid.NewGuid().ToString("N")
            : update.ToolCallId;

        var toolState = GetOrAddToolCall(callId, update.ToolName, update.Arguments);
        return CreateEvent(
            AgentMessageEventType.ToolCall,
            toolName: toolState.Name,
            toolCallId: toolState.CallId,
            toolArguments: toolState.Arguments);
    }

    public AgentMessageEvent Collect(TaskToolCallResponseEvent update)
    {
        var callId = string.IsNullOrWhiteSpace(update.ToolCallId)
            ? Guid.NewGuid().ToString("N")
            : update.ToolCallId;

        var toolState = GetOrAddToolCall(callId, update.ToolName, null);
        toolState.Result = update.Response;
        toolState.Status = ToolInvocationStatus.Done;

        return CreateEvent(
            AgentMessageEventType.ToolResult,
            toolName: toolState.Name,
            toolCallId: toolState.CallId,
            toolResult: toolState.Result);
    }

    public AgentMessageEvent? Collect(TaskUsageUpdatedEvent update)
    {
        if (update.UsageContent.Details == null)
            return null;

        InputTokens = (InputTokens ?? 0) + (int)(update.UsageContent.Details.InputTokenCount ?? 0);
        OutputTokens = (OutputTokens ?? 0) + (int)(update.UsageContent.Details.OutputTokenCount ?? 0);

        return CreateEvent(
            AgentMessageEventType.UsageUpdated,
            usage: BuildUsage());
    }

    /// <summary>
    /// zh-CN: 创建不携带额外负载的生命周期事件，例如 Started。
    /// en: Creates lifecycle events that do not need extra payload, such as Started.
    /// </summary>
    public AgentMessageEvent CreateLifecycleEvent(AgentMessageEventType eventType)
        => CreateEvent(eventType);

    /// <summary>
    /// zh-CN: 记录一次可重试失败，并附带当前耗时快照供外部展示与诊断。
    /// en: Records a retryable failure together with the current timing snapshot for UI and diagnostics.
    /// </summary>
    public AgentMessageEvent CreateRetryEvent(Exception exception, Stopwatch stopwatch)
        => CreateEvent(
            AgentMessageEventType.RetryScheduled,
            error: exception.Message,
            timing: BuildTiming(stopwatch));

    /// <summary>
    /// zh-CN: 创建完成终态事件，并冻结累计的文本、思考、usage 与耗时信息。
    /// en: Creates the completed terminal event and freezes the accumulated text, reasoning, usage, and timing data.
    /// </summary>
    public AgentMessageEvent CreateCompletedEvent(Stopwatch stopwatch)
        => CreateEvent(
            AgentMessageEventType.Completed,
            text: _content.ToString(),
            thoughtText: _thinking.ToString(),
            usage: BuildUsage(),
            timing: BuildTiming(stopwatch),
            isTerminal: true);

    /// <summary>
    /// zh-CN: 创建取消终态事件，同时保留执行过程中已采集到的 usage 与 timing 快照。
    /// en: Creates the cancelled terminal event while preserving any usage and timing snapshots collected before cancellation.
    /// </summary>
    public AgentMessageEvent CreateCancelledEvent(Stopwatch stopwatch)
        => CreateEvent(
            AgentMessageEventType.Cancelled,
            error: "Message execution cancelled.",
            usage: BuildUsage(),
            timing: BuildTiming(stopwatch),
            isTerminal: true);

    /// <summary>
    /// zh-CN: 创建错误终态事件，让调用方在失败时仍能读取累计的 usage 与 timing 信息。
    /// en: Creates the error terminal event so callers can still inspect accumulated usage and timing on failure.
    /// </summary>
    public AgentMessageEvent CreateErrorEvent(Exception exception, Stopwatch stopwatch)
        => CreateEvent(
            AgentMessageEventType.Error,
            error: exception.Message,
            usage: BuildUsage(),
            timing: BuildTiming(stopwatch),
            isTerminal: true);

    /// <summary>
    /// zh-CN: 将当前尝试的全部累计状态冻结为终态摘要，工具调用顺序与流式观察顺序保持一致。
    /// en: Freezes all accumulated state for the current attempt into a terminal summary while preserving streamed tool-call order.
    /// </summary>
    public MessageExecutionSummary CreateSummary(
        bool success,
        bool cancelled,
        string? error,
        Stopwatch stopwatch)
        => new(
            messageId,
            scene,
            context,
            success,
            cancelled,
            attempt,
            agentRole,
            model,
            _content.ToString(),
            _thinking.ToString(),
            BuildUsage(),
            BuildTiming(stopwatch),
            _toolCallOrder
                .Select(tool => new ToolInvocationSnapshot(
                    tool.CallId,
                    tool.Name,
                    tool.Arguments,
                    tool.Result,
                    tool.Error,
                    tool.Status))
                .ToList(),
            error);

    /// <summary>
    /// zh-CN: 为所有运行时事件统一补齐共享元数据，并在创建时戳上当前 UTC 时间。
    /// en: Populates the shared metadata for runtime events and stamps them with the current UTC time at creation.
    /// </summary>
    private AgentMessageEvent CreateEvent(
        AgentMessageEventType eventType,
        string? text = null,
        string? thoughtText = null,
        string? error = null,
        string? toolName = null,
        string? toolCallId = null,
        string? toolArguments = null,
        string? toolResult = null,
        MessageUsageSnapshot? usage = null,
        MessageTimingSnapshot? timing = null,
        bool isTerminal = false)
        => new()
        {
            MessageId = messageId,
            EventType = eventType,
            Scene = scene,
            Context = context,
            OccurredAt = DateTimeOffset.UtcNow,
            Attempt = attempt,
            Text = text,
            ThoughtText = thoughtText,
            Error = error,
            ProviderMessageId = ProviderMessageId,
            AgentRole = agentRole,
            Model = model,
            ToolName = toolName,
            ToolCallId = toolCallId,
            ToolArguments = toolArguments,
            ToolResult = toolResult,
            Usage = usage,
            Timing = timing,
            IsTerminal = isTerminal
        };

    /// <summary>
    /// zh-CN: 仅在已经观察到 token 统计时创建 usage 快照，避免向外暴露伪造的零值统计。
    /// en: Creates a usage snapshot only after token counts have been observed so callers do not see fabricated zero totals.
    /// </summary>
    private MessageUsageSnapshot? BuildUsage()
    {
        if (InputTokens == null && OutputTokens == null)
            return null;

        return new MessageUsageSnapshot(
            InputTokens,
            OutputTokens,
            (InputTokens ?? 0) + (OutputTokens ?? 0));
    }

    /// <summary>
    /// zh-CN: 从当前秒表与首 token 记录生成耗时快照。
    /// en: Builds the timing snapshot from the current stopwatch and recorded first-token latency.
    /// </summary>
    private MessageTimingSnapshot BuildTiming(Stopwatch stopwatch)
        => new(stopwatch.ElapsedMilliseconds, FirstTokenMs);

    /// <summary>
    /// zh-CN: 维护按 callId 聚合的工具调用状态，使分段到达的调用元数据和结果能够合并到同一条记录。
    /// en: Maintains tool-call state grouped by callId so partial metadata and later results merge into the same logical invocation.
    /// </summary>
    private ToolInvocationState GetOrAddToolCall(
        string callId,
        string? name,
        string? arguments)
    {
        if (_toolCallsById.TryGetValue(callId, out var existing))
        {
            if (!string.IsNullOrWhiteSpace(name))
                existing.Name = name;

            if (!string.IsNullOrWhiteSpace(arguments))
                existing.Arguments = arguments;

            return existing;
        }

        // zh-CN: 工具调用顺序单独记录，保证最终摘要与流式观察到的顺序一致。
        // en: Track tool-call order separately so the terminal summary preserves the streamed observation order.
        var toolState = new ToolInvocationState
        {
            CallId = callId,
            Name = string.IsNullOrWhiteSpace(name) ? "unknown" : name,
            Arguments = arguments,
            Status = ToolInvocationStatus.Calling
        };

        _toolCallsById[callId] = toolState;
        _toolCallOrder.Add(toolState);
        return toolState;
    }

    /// <summary>
    /// zh-CN: 将函数参数标准化为 JSON 字符串，以便事件回放、持久化和调试统一处理。
    /// en: Normalizes function arguments into a JSON string so replay, persistence, and diagnostics can treat them consistently.
    /// </summary>
    private static string? SerializeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
            return null;

        return JsonSerializer.Serialize(arguments);
    }

    /// <summary>
    /// zh-CN: 将工具结果转换为适合展示与存储的文本形式，保留结构化对象的 JSON 表达。
    /// en: Converts tool results into a displayable and storable text form while preserving structured objects as JSON.
    /// </summary>
    private static string? ToDisplayText(object? value)
        => value switch
        {
            null => null,
            string text => text,
            // zh-CN: 非字符串结果统一序列化，方便持久化和调试。
            // en: Serialize non-string results so they remain easy to persist and inspect.
            _ => JsonSerializer.Serialize(value)
        };
}
