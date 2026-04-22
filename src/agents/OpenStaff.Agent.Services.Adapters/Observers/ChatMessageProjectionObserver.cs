using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent.Services;
using OpenStaff.Entities;
using OpenStaff.Core.Notifications;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Repositories;

namespace OpenStaff.Agent.Services.Adapters;

/// <summary>
/// zh-CN: 持久化运行时消息的终态 assistant 回复，并发出最终会话消息事件。
/// en: Persists the terminal assistant reply for a runtime message and emits the final session message event.
/// </summary>
public sealed class ChatMessageProjectionObserver : IAgentMessageObserver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INotificationService _notificationService;
    private readonly ConcurrentDictionary<Guid, ProjectionState> _states = new();

    /// <summary>
    /// zh-CN: 初始化聊天消息终态投影观察者。
    /// en: Initializes the terminal chat-message projection observer.
    /// </summary>
    public ChatMessageProjectionObserver(
        IServiceScopeFactory scopeFactory,
        INotificationService notificationService)
    {
        _scopeFactory = scopeFactory;
        _notificationService = notificationService;
    }

    /// <summary>
    /// zh-CN: 消费运行时事件，并在完成时投影出最终聊天消息。
    /// en: Consumes runtime events and projects the final chat message on completion.
    /// </summary>
    public async Task PublishAsync(AgentMessageEvent messageEvent, CancellationToken cancellationToken)
    {
        if (ShouldSkipFinalProjection(messageEvent.Context))
            return;

        if (!messageEvent.Context.SessionId.HasValue || !messageEvent.Context.FrameId.HasValue)
            return;

        var state = _states.GetOrAdd(messageEvent.MessageId, _ => new ProjectionState());
        state.Apply(messageEvent);

        switch (messageEvent.EventType)
        {
            case AgentMessageEventType.Completed:
                await PersistMessageAsync(messageEvent, state, cancellationToken);
                _states.TryRemove(messageEvent.MessageId, out _);
                break;
            case AgentMessageEventType.Error:
            case AgentMessageEventType.Cancelled:
                _states.TryRemove(messageEvent.MessageId, out _);
                break;
        }
    }

    /// <summary>
    /// zh-CN: 将完成态运行结果落库为 assistant 消息，并向会话流广播最终消息事件。
    /// en: Persists the completed runtime result as an assistant message and broadcasts the final message event to the session stream.
    /// </summary>
    private async Task PersistMessageAsync(
        AgentMessageEvent messageEvent,
        ProjectionState state,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var chatMessages = serviceProvider.GetRequiredService<IChatMessageRepository>();
        var repositoryContext = serviceProvider.GetRequiredService<IRepositoryContext>();
        var frameId = messageEvent.Context.FrameId!.Value;
        var sessionId = messageEvent.Context.SessionId!.Value;

        var maxSequence = await chatMessages
            .Where(item => item.FrameId == frameId)
            .MaxAsync(item => (int?)item.SequenceNo, cancellationToken) ?? 0;

        // zh-CN: 终态 assistant 消息始终挂在当前 Frame 的首条消息下，保持一轮运行共享同一根节点。
        // en: The terminal assistant message is always attached to the first message in the frame so a single run keeps a stable root node.
        var parentMessageId = await chatMessages
            .AsNoTracking()
            .Where(item => item.FrameId == frameId)
            .OrderBy(item => item.SequenceNo)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var savedMessage = new ChatMessage
        {
            Id = messageEvent.MessageId,
            FrameId = frameId,
            SessionId = sessionId,
            ExecutionPackageId = messageEvent.Context.ExecutionPackageId,
            OriginatingFrameId = messageEvent.Context.SourceFrameId ?? frameId,
            ParentMessageId = parentMessageId,
            Role = MessageRoles.Assistant,
            ProjectAgentRoleId = messageEvent.Context.ProjectAgentRoleId,
            Content = state.Content.ToString(),
            ContentType = MessageContentTypes.Text,
            SequenceNo = maxSequence + 1,
            TokenUsage = state.Usage == null ? null : JsonSerializer.Serialize(state.Usage),
            DurationMs = state.Timing?.TotalMs,
            CreatedAt = messageEvent.OccurredAt.UtcDateTime
        };

        chatMessages.Add(savedMessage);
        await repositoryContext.SaveChangesAsync(cancellationToken);

        await _notificationService.PublishSessionEventAsync(
            sessionId,
            new SessionEvent
            {
                SessionId = sessionId,
                ExecutionPackageId = messageEvent.Context.ExecutionPackageId,
                FrameId = frameId,
                MessageId = savedMessage.Id,
                SourceFrameId = messageEvent.Context.SourceFrameId ?? frameId,
                EventType = SessionEventTypes.Message,
                Payload = JsonSerializer.Serialize(new
                {
                    messageId = savedMessage.Id,
                    parentMessageId = savedMessage.ParentMessageId,
                    role = savedMessage.Role,
                    agentRoleId = savedMessage.AgentRoleId,
                    projectAgentRoleId = savedMessage.ProjectAgentRoleId,
                    content = savedMessage.Content,
                    success = true,
                    usage = state.Usage,
                    timing = state.Timing,
                    model = state.Model
                }),
                CreatedAt = messageEvent.OccurredAt.UtcDateTime
            },
            cancellationToken);
    }

    /// <summary>
    /// zh-CN: 读取运行时扩展标记，判断当前消息是否只需要流式输出而不应生成持久化 ChatMessage。
    /// en: Reads the runtime extension flag to determine whether the message should remain streaming-only instead of creating a persisted ChatMessage.
    /// </summary>
    private static bool ShouldSkipFinalProjection(MessageContext context)
    {
        // zh-CN: 某些运行只需要流式事件或监控投影，不应重复生成 ChatMessage。
        // en: Some runs only need streaming events or monitoring projections and should not materialize an extra ChatMessage.
        if (context.Extra == null
            || !context.Extra.TryGetValue("skip_final_projection", out var value))
        {
            return false;
        }

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// zh-CN: 缓存一条消息在完成前累计到的流式内容、计量与角色信息。
    /// en: Buffers the streamed content, metrics, and role metadata accumulated for a message before completion.
    /// </summary>
    private sealed class ProjectionState
    {
        public StringBuilder Content { get; } = new();
        public StringBuilder Thinking { get; } = new();
        public MessageUsageSnapshot? Usage { get; private set; }
        public MessageTimingSnapshot? Timing { get; private set; }
        public string? AgentRole { get; private set; }
        public string? Model { get; private set; }

        /// <summary>
        /// zh-CN: 合并增量事件，并在只收到终态时回填完整文本，避免遗漏未经过流式分片的完成结果。
        /// en: Merges incremental events and backfills full text on completion when no prior stream chunks were seen, preventing loss of non-streamed final output.
        /// </summary>
        public void Apply(AgentMessageEvent messageEvent)
        {
            switch (messageEvent.EventType)
            {
                case AgentMessageEventType.ContentChunk:
                    if (!string.IsNullOrWhiteSpace(messageEvent.Text))
                        Content.Append(messageEvent.Text);
                    break;
                case AgentMessageEventType.ThinkingChunk:
                    if (!string.IsNullOrWhiteSpace(messageEvent.ThoughtText))
                        Thinking.Append(messageEvent.ThoughtText);
                    break;
                case AgentMessageEventType.Completed:
                    if (Content.Length == 0 && !string.IsNullOrWhiteSpace(messageEvent.Text))
                        Content.Append(messageEvent.Text);
                    if (Thinking.Length == 0 && !string.IsNullOrWhiteSpace(messageEvent.ThoughtText))
                        Thinking.Append(messageEvent.ThoughtText);
                    break;
            }

            Usage = messageEvent.Usage ?? Usage;
            Timing = messageEvent.Timing ?? Timing;
            AgentRole = messageEvent.AgentRole ?? AgentRole;
            Model = messageEvent.Model ?? Model;
        }
    }
}
