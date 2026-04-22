namespace OpenStaff.Agent.Services;

/// <summary>
/// zh-CN: 协调异步智能体执行，并向调用方暴露按消息划分的运行时状态。
/// en: Coordinates asynchronous agent execution and exposes per-message runtime state to callers.
/// </summary>
public interface IAgentService
{
    /// <summary>
    /// zh-CN: 接收一条新消息并在后台启动运行时执行流水线。
    /// en: Accepts a new message for execution and starts the runtime pipeline in the background.
    /// </summary>
    Task<CreateMessageResponse> CreateMessageAsync(
        CreateMessageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// zh-CN: 尝试获取活动消息处理器，以便调用方订阅流式事件或等待完成。
    /// en: Tries to resolve an active handler so callers can observe streaming events or await completion.
    /// </summary>
    bool TryGetMessageHandler(Guid messageId, out MessageHandler? handler);

    /// <summary>
    /// zh-CN: 请求取消正在运行的消息。
    /// en: Requests cancellation for a running message.
    /// </summary>
    Task<bool> CancelMessageAsync(Guid messageId);

    /// <summary>
    /// zh-CN: 移除内存中的处理器并释放与该消息关联的资源。
    /// en: Removes the in-memory handler and releases resources associated with the message.
    /// </summary>
    bool RemoveMessageHandler(Guid messageId);
}
