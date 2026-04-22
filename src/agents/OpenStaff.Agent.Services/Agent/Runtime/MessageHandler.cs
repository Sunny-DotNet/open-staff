using System.Reactive.Subjects;

namespace OpenStaff.Agent.Services;

/// <summary>
/// zh-CN: 保存单条逻辑消息的内存回放流与完成任务。
/// en: Holds the in-memory replay stream and completion task for a single logical message execution.
/// </summary>
/// <param name="messageId">zh-CN: 该处理器对应的逻辑消息标识。 en: The logical message identifier owned by this handler.</param>
/// <param name="scene">zh-CN: 该消息所属的运行场景。 en: The runtime scene associated with the message.</param>
/// <param name="context">zh-CN: 应附着到流式事件与终态摘要上的执行上下文。 en: The execution context attached to streamed events and terminal summaries.</param>
public sealed class MessageHandler(Guid messageId, MessageScene scene, MessageContext context) : IDisposable
{
    private readonly ReplaySubject<AgentMessageEvent> _eventsSubject = new();
    private readonly TaskCompletionSource<MessageExecutionSummary> _completionSource = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// zh-CN: 获取该处理器跟踪的逻辑消息标识。
    /// en: Gets the logical message identifier tracked by this handler.
    /// </summary>
    public Guid MessageId => messageId;

    /// <summary>
    /// zh-CN: 获取该消息所属的场景。
    /// en: Gets the scene that owns the message.
    /// </summary>
    public MessageScene Scene => scene;

    /// <summary>
    /// zh-CN: 获取与消息关联的执行上下文。
    /// en: Gets the execution context associated with the message.
    /// </summary>
    public MessageContext Context => context;

    /// <summary>
    /// zh-CN: 获取该消息的可回放运行时事件流。
    /// en: Gets a replayable stream of runtime events for this message.
    /// </summary>
    public IObservable<AgentMessageEvent> Events => _eventsSubject;

    /// <summary>
    /// zh-CN: 获取在运行时到达终态后完成的任务。
    /// en: Gets the task that completes when the runtime reaches a terminal state.
    /// </summary>
    public Task<MessageExecutionSummary> Completion => _completionSource.Task;

    /// <summary>
    /// zh-CN: 将运行时事件推入可回放流，调用方需要自行保证发布顺序。
    /// en: Pushes a runtime event into the replayable stream, with ordering guaranteed by the caller.
    /// </summary>
    internal void Publish(AgentMessageEvent messageEvent) => _eventsSubject.OnNext(messageEvent);

    /// <summary>
    /// zh-CN: 先完成终态摘要任务，再关闭事件流，确保订阅方能够同时拿到终态事件和完成结果。
    /// en: Completes the terminal summary task before closing the event stream so subscribers can observe both the final event and the completion result.
    /// </summary>
    internal void Complete(MessageExecutionSummary summary)
    {
        _completionSource.TrySetResult(summary);
        _eventsSubject.OnCompleted();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_completionSource.Task.IsCompleted)
            _completionSource.TrySetCanceled();

        _eventsSubject.Dispose();
    }
}
