using System.Text.Json;
using OpenStaff.Agent.Services;
using OpenStaff.Entities;
using OpenStaff.Core.Notifications;

namespace OpenStaff.Agent.Services.Adapters;

/// <summary>
/// zh-CN: 将运行时流式事件映射为通过通知通道推送的会话事件。
/// en: Projects runtime streaming events into session events pushed over the notification channel.
/// </summary>
public sealed class SessionStreamingAgentMessageObserver : IAgentMessageObserver
{
    private readonly INotificationService _notificationService;

    /// <summary>
    /// zh-CN: 初始化会话流式事件观察者。
    /// en: Initializes the session streaming observer.
    /// </summary>
    public SessionStreamingAgentMessageObserver(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// zh-CN: 将运行时事件发布到会话流。
    /// en: Publishes a runtime event to the session stream.
    /// </summary>
    public Task PublishAsync(AgentMessageEvent messageEvent, CancellationToken cancellationToken)
    {
        if (!messageEvent.Context.SessionId.HasValue)
            return Task.CompletedTask;

        var sessionEvent = Map(messageEvent);
        if (sessionEvent == null)
            return Task.CompletedTask;

        return _notificationService.PublishSessionEventAsync(
            messageEvent.Context.SessionId.Value,
            sessionEvent,
            cancellationToken);
    }

    /// <summary>
    /// zh-CN: 将内部运行时事件映射为前端会消费的会话事件，并显式过滤掉仅供本地回放的生命周期事件。
    /// en: Maps internal runtime events to the session events consumed by the frontend and explicitly filters lifecycle steps that should remain local-only.
    /// </summary>
    private static SessionEvent? Map(AgentMessageEvent messageEvent)
    {
        // zh-CN: 这里只投影前端真正需要实时消费的事件，Accepted/Started 等内部事件由运行时本地回放承担。
        // en: Only project events that the frontend consumes in real time; internal lifecycle steps like Accepted/Started stay in local runtime replay.
        return messageEvent.EventType switch
        {
            AgentMessageEventType.ThinkingChunk => CreateSessionEvent(
                messageEvent,
                SessionEventTypes.StreamingThinking,
                new
                {
                    token = messageEvent.ThoughtText,
                    agent = messageEvent.AgentRole
                }),
            AgentMessageEventType.ContentChunk => CreateSessionEvent(
                messageEvent,
                SessionEventTypes.StreamingToken,
                new
                {
                    token = messageEvent.Text,
                    agent = messageEvent.AgentRole
                }),
            AgentMessageEventType.ToolCall => CreateSessionEvent(
                messageEvent,
                SessionEventTypes.ToolCall,
                new
                {
                    agent = messageEvent.AgentRole,
                    name = messageEvent.ToolName,
                    toolCallId = messageEvent.ToolCallId,
                    arguments = messageEvent.ToolArguments
                }),
            AgentMessageEventType.ToolResult => CreateSessionEvent(
                messageEvent,
                SessionEventTypes.ToolResult,
                new
                {
                    agent = messageEvent.AgentRole,
                    name = messageEvent.ToolName,
                    toolCallId = messageEvent.ToolCallId,
                    result = messageEvent.ToolResult
                }),
            AgentMessageEventType.ToolError => CreateSessionEvent(
                messageEvent,
                SessionEventTypes.ToolError,
                new
                {
                    agent = messageEvent.AgentRole,
                    name = messageEvent.ToolName,
                    toolCallId = messageEvent.ToolCallId,
                    error = messageEvent.Error
                }),
            AgentMessageEventType.RetryScheduled => CreateSessionEvent(
                messageEvent,
                SessionEventTypes.Action,
                new
                {
                    type = "retry_scheduled",
                    attempt = messageEvent.Attempt,
                    error = messageEvent.Error
                }),
            AgentMessageEventType.Completed => CreateSessionEvent(
                messageEvent,
                SessionEventTypes.StreamingDone,
                new
                {
                    messageId = messageEvent.MessageId,
                    agent = messageEvent.AgentRole,
                    model = messageEvent.Model,
                    usage = messageEvent.Usage,
                    timing = messageEvent.Timing
                }),
            AgentMessageEventType.Error => CreateSessionEvent(
                messageEvent,
                SessionEventTypes.Error,
                new
                {
                    message = messageEvent.Error,
                    error = messageEvent.Error,
                    attempt = messageEvent.Attempt,
                    agent = messageEvent.AgentRole,
                    model = messageEvent.Model
                }),
            AgentMessageEventType.Cancelled => CreateSessionEvent(
                messageEvent,
                SessionEventTypes.Error,
                new
                {
                    message = messageEvent.Error ?? "Message execution cancelled.",
                    error = messageEvent.Error ?? "Message execution cancelled.",
                    cancelled = true,
                    attempt = messageEvent.Attempt,
                    agent = messageEvent.AgentRole,
                    model = messageEvent.Model
                }),
            _ => null
        };
    }

    /// <summary>
    /// zh-CN: 用当前运行时标识构造通知事件信封，并把具体负载序列化为稳定的 JSON。
    /// en: Builds the notification envelope from the current runtime identifiers and serializes the payload into stable JSON.
    /// </summary>
    private static SessionEvent CreateSessionEvent(
        AgentMessageEvent messageEvent,
        string eventType,
        object payload)
    {
        return new SessionEvent
        {
            SessionId = messageEvent.Context.SessionId!.Value,
            ExecutionPackageId = messageEvent.Context.ExecutionPackageId,
            FrameId = messageEvent.Context.FrameId,
            MessageId = messageEvent.MessageId,
            SourceFrameId = messageEvent.Context.SourceFrameId ?? messageEvent.Context.FrameId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            CreatedAt = messageEvent.OccurredAt.UtcDateTime
        };
    }
}
