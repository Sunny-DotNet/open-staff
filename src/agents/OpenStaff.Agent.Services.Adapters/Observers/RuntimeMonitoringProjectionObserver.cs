using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Repositories;

namespace OpenStaff.Agent.Services.Adapters;

/// <summary>
/// zh-CN: 将运行时生命周期事件投影为持久化监控事件和任务运行时元数据。
/// en: Projects runtime lifecycle events into persisted monitoring events and task runtime metadata.
/// </summary>
public sealed class RuntimeMonitoringProjectionObserver : IAgentMessageObserver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<Guid, ProjectionState> _states = new();

    /// <summary>
    /// zh-CN: 初始化运行时监控投影观察者。
    /// en: Initializes the runtime monitoring projection observer.
    /// </summary>
    public RuntimeMonitoringProjectionObserver(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// zh-CN: 持久化监控事件并同步更新关联任务的运行时元数据。
    /// en: Persists monitoring events and keeps associated task runtime metadata in sync.
    /// </summary>
    public async Task PublishAsync(AgentMessageEvent messageEvent, CancellationToken cancellationToken)
    {
        if (!messageEvent.Context.ProjectId.HasValue)
        {
            CleanupIfTerminal(messageEvent);
            return;
        }

        var state = _states.GetOrAdd(messageEvent.MessageId, _ => new ProjectionState());

        using var scope = _scopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var agentEvents = serviceProvider.GetRequiredService<IAgentEventRepository>();
        var tasks = serviceProvider.GetRequiredService<ITaskItemRepository>();
        var repositoryContext = serviceProvider.GetRequiredService<IRepositoryContext>();

        var projectedEvent = CreateAgentEvent(messageEvent, state);
        if (projectedEvent != null)
        {
            agentEvents.Add(projectedEvent);
            state.Apply(projectedEvent, messageEvent.ToolCallId);
        }

        if (messageEvent.Context.TaskId.HasValue)
        {
            var task = await tasks.FindAsync(new object[] { messageEvent.Context.TaskId.Value }, cancellationToken);
            if (task != null)
            {
                // zh-CN: 任务元数据保存的是“最新可见状态”，因此每个事件都以增量方式覆盖相关字段。
                // en: Task metadata stores the latest visible runtime state, so each event incrementally refreshes only the relevant fields.
                var metadata = RuntimeProjectionMetadataMapper.ParseTaskMetadata(task.Metadata)
                    ?? new TaskItemRuntimeMetadata();
                ApplyTaskMetadata(metadata, messageEvent);
                task.Metadata = JsonSerializer.Serialize(metadata);
                task.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (projectedEvent != null || messageEvent.Context.TaskId.HasValue)
            await repositoryContext.SaveChangesAsync(cancellationToken);

        CleanupIfTerminal(messageEvent);
    }

    /// <summary>
    /// zh-CN: 将运行时事件映射为需要持久化的监控事件，纯流式分片等噪声事件会被跳过。
    /// en: Maps runtime events into persisted monitoring events and skips noise-only updates such as pure streaming chunks.
    /// </summary>
    private static AgentEvent? CreateAgentEvent(AgentMessageEvent messageEvent, ProjectionState state)
    {
        var eventType = messageEvent.EventType switch
        {
            AgentMessageEventType.Started => EventTypes.RunStarted,
            AgentMessageEventType.RetryScheduled => EventTypes.RunRetryScheduled,
            AgentMessageEventType.ToolCall => EventTypes.ToolCall,
            AgentMessageEventType.ToolResult => EventTypes.ToolResult,
            AgentMessageEventType.ToolError => EventTypes.ToolError,
            AgentMessageEventType.Completed => EventTypes.Message,
            AgentMessageEventType.Error => EventTypes.Error,
            AgentMessageEventType.Cancelled => EventTypes.RunCancelled,
            _ => null
        };

        if (eventType == null)
            return null;

        return new AgentEvent
        {
            Id = Guid.NewGuid(),
            ProjectId = messageEvent.Context.ProjectId!.Value,
            ProjectAgentRoleId = messageEvent.Context.ProjectAgentRoleId,
            ParentEventId = ResolveParentEventId(messageEvent, state),
            EventType = eventType,
            Content = BuildContent(messageEvent),
            Metadata = JsonSerializer.Serialize(BuildMetadata(messageEvent)),
            CreatedAt = messageEvent.OccurredAt.UtcDateTime
        };
    }

    /// <summary>
    /// zh-CN: 为监控事件确定父节点，保证一次运行下的工具调用与终态事件形成稳定树结构。
    /// en: Resolves the parent id for monitoring events so tool calls and terminal events form a stable tree within a single run.
    /// </summary>
    private static Guid? ResolveParentEventId(AgentMessageEvent messageEvent, ProjectionState state)
    {
        // zh-CN: 工具结果/错误挂到对应的 ToolCall 事件下，其余终态事件则挂到本轮 RunStarted 根事件下，形成稳定树形结构。
        // en: Tool results/errors attach to their ToolCall event, while other terminal lifecycle events attach to the RunStarted root for a stable tree.
        return messageEvent.EventType switch
        {
            AgentMessageEventType.ToolResult or AgentMessageEventType.ToolError
                when !string.IsNullOrWhiteSpace(messageEvent.ToolCallId)
                && state.ToolEvents.TryGetValue(messageEvent.ToolCallId, out var toolEventId) => toolEventId,
            AgentMessageEventType.ToolCall or AgentMessageEventType.RetryScheduled or AgentMessageEventType.Completed
                or AgentMessageEventType.Error or AgentMessageEventType.Cancelled => state.RootEventId,
            _ => null
        };
    }

    /// <summary>
    /// zh-CN: 生成监控面板可直接展示的事件文本，必要时对长内容做安全截断。
    /// en: Builds the human-readable event text used by monitoring views, truncating long content when necessary.
    /// </summary>
    private static string BuildContent(AgentMessageEvent messageEvent)
    {
        return messageEvent.EventType switch
        {
            AgentMessageEventType.Started => $"开始执行：{messageEvent.AgentRole  ?? "agent"}",
            AgentMessageEventType.RetryScheduled => messageEvent.Error ?? "运行时已安排自动重试。",
            AgentMessageEventType.ToolCall => $"调用工具：{messageEvent.ToolName ?? "unknown"}",
            AgentMessageEventType.ToolResult => $"工具返回：{messageEvent.ToolName ?? "unknown"}",
            AgentMessageEventType.ToolError => messageEvent.Error ?? $"工具调用失败：{messageEvent.ToolName ?? "unknown"}",
            AgentMessageEventType.Completed => Limit(messageEvent.Text, 2000),
            AgentMessageEventType.Error => messageEvent.Error ?? "运行失败。",
            AgentMessageEventType.Cancelled => messageEvent.Error ?? "运行已取消。",
            _ => string.Empty
        };
    }

    /// <summary>
    /// zh-CN: 构造可回溯的结构化监控元数据，供后续解析、聚合和任务侧联动使用。
    /// en: Builds the structured monitoring metadata used for later parsing, aggregation, and task-side synchronization.
    /// </summary>
    private static AgentEventMetadataPayload BuildMetadata(AgentMessageEvent messageEvent)
    {
        var scene = RuntimeProjectionMetadataMapper.NormalizeScene(messageEvent.Scene);
        return new AgentEventMetadataPayload
        {
            TaskId = messageEvent.Context.TaskId,
            SessionId = messageEvent.Context.SessionId,
            FrameId = messageEvent.Context.FrameId,
            MessageId = messageEvent.MessageId,
            ExecutionPackageId = messageEvent.Context.ExecutionPackageId,
            Scene = scene,
            EntryKind = messageEvent.Context.EntryKind,
            ProjectAgentRoleId = messageEvent.Context.ProjectAgentRoleId,
            Model = messageEvent.Model,
            ToolName = messageEvent.ToolName,
            ToolCallId = messageEvent.ToolCallId,
            Status = MapRuntimeStatus(messageEvent),
            SourceFrameId = messageEvent.Context.SourceFrameId ?? messageEvent.Context.FrameId,
            SourceEffectIndex = null,
            Source = "runtime_projection",
            Detail = BuildDetail(messageEvent),
            Attempt = messageEvent.Attempt,
            MaxAttempts = TaskItemRuntimeMetadata.MaxAttempts,
            InputTokens = messageEvent.Usage?.InputTokens,
            OutputTokens = messageEvent.Usage?.OutputTokens,
            TotalTokens = messageEvent.Usage?.TotalTokens,
            DurationMs = messageEvent.Timing?.TotalMs,
            FirstTokenMs = messageEvent.Timing?.FirstTokenMs
        };
    }

    /// <summary>
    /// zh-CN: 用当前运行时事件增量刷新任务元数据，只覆盖本次事件真正更新过的可见字段。
    /// en: Incrementally refreshes task metadata from the current runtime event, touching only the user-visible fields updated by that event.
    /// </summary>
    private static void ApplyTaskMetadata(TaskItemRuntimeMetadata metadata, AgentMessageEvent messageEvent)
    {
        metadata.SessionId ??= messageEvent.Context.SessionId;
        metadata.ExecutionPackageId ??= messageEvent.Context.ExecutionPackageId;
        metadata.FrameId ??= messageEvent.Context.FrameId;
        metadata.MessageId = messageEvent.MessageId;
        metadata.Scene ??= RuntimeProjectionMetadataMapper.NormalizeScene(messageEvent.Scene);
        metadata.EntryKind ??= messageEvent.Context.EntryKind;
        metadata.TargetProjectAgentRoleId ??= messageEvent.Context.ProjectAgentRoleId;
        metadata.SourceFrameId ??= messageEvent.Context.SourceFrameId ?? messageEvent.Context.FrameId;
        metadata.Model = messageEvent.Model ?? metadata.Model;
        metadata.AttemptCount = Math.Max(metadata.AttemptCount, Math.Max(messageEvent.Attempt - 1, 0));
        metadata.TotalTokens = messageEvent.Usage?.TotalTokens ?? metadata.TotalTokens;
        metadata.DurationMs = messageEvent.Timing?.TotalMs ?? metadata.DurationMs;
        metadata.FirstTokenMs = messageEvent.Timing?.FirstTokenMs ?? metadata.FirstTokenMs;

        switch (messageEvent.EventType)
        {
            case AgentMessageEventType.Started:
                metadata.LastStatus = TaskItemStatus.InProgress;
                break;
            case AgentMessageEventType.RetryScheduled:
                metadata.LastStatus = "retry_scheduled";
                metadata.LastError = messageEvent.Error;
                break;
            case AgentMessageEventType.Completed:
                metadata.LastStatus = TaskItemStatus.Done;
                metadata.LastResult = messageEvent.Text;
                metadata.LastError = null;
                break;
            case AgentMessageEventType.Error:
                metadata.LastStatus = "runtime_error";
                metadata.LastError = messageEvent.Error;
                break;
            case AgentMessageEventType.Cancelled:
                metadata.LastStatus = TaskItemStatus.Cancelled;
                metadata.LastError = messageEvent.Error;
                break;
            case AgentMessageEventType.ToolError:
                metadata.LastError = messageEvent.Error;
                break;
        }
    }

    /// <summary>
    /// zh-CN: 在终态事件到达后移除内存投影状态，防止长时间运行的节点持续累积历史映射。
    /// en: Removes the in-memory projection state once a terminal event arrives so long-lived nodes do not accumulate stale mappings.
    /// </summary>
    private void CleanupIfTerminal(AgentMessageEvent messageEvent)
    {
        if (messageEvent.EventType is AgentMessageEventType.Completed or AgentMessageEventType.Error or AgentMessageEventType.Cancelled)
            _states.TryRemove(messageEvent.MessageId, out _);
    }

    /// <summary>
    /// zh-CN: 将运行时事件类型归一化为监控与任务元数据共享的状态字符串。
    /// en: Normalizes runtime event types into the status strings shared by monitoring views and task metadata.
    /// </summary>
    private static string MapRuntimeStatus(AgentMessageEvent messageEvent) => messageEvent.EventType switch
    {
        AgentMessageEventType.Started => TaskItemStatus.InProgress,
        AgentMessageEventType.RetryScheduled => "retry_scheduled",
        AgentMessageEventType.ToolCall => "calling",
        AgentMessageEventType.ToolResult => "done",
        AgentMessageEventType.ToolError => "error",
        AgentMessageEventType.Completed => TaskItemStatus.Done,
        AgentMessageEventType.Error => "runtime_error",
        AgentMessageEventType.Cancelled => TaskItemStatus.Cancelled,
        _ => string.Empty
    };

    /// <summary>
    /// zh-CN: 提取需要额外保存到监控元数据中的细节字段，例如工具参数、结果或错误文本。
    /// en: Extracts the detail payload that should be stored in monitoring metadata, such as tool arguments, results, or error text.
    /// </summary>
    private static string? BuildDetail(AgentMessageEvent messageEvent) => messageEvent.EventType switch
    {
        AgentMessageEventType.ToolCall => Limit(messageEvent.ToolArguments, 2000),
        AgentMessageEventType.ToolResult => Limit(messageEvent.ToolResult, 2000),
        AgentMessageEventType.ToolError => messageEvent.Error,
        AgentMessageEventType.RetryScheduled => messageEvent.Error,
        AgentMessageEventType.Error => messageEvent.Error,
        AgentMessageEventType.Cancelled => messageEvent.Error,
        _ => null
    };

    /// <summary>
    /// zh-CN: 将用于监控展示的长文本裁剪到安全长度，并把空白输入统一归一为空字符串。
    /// en: Trims long text to a monitoring-safe length and normalizes blank input to an empty string.
    /// </summary>
    private static string Limit(string? content, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        return content.Length <= maxLength
            ? content
            : content[..maxLength].TrimEnd() + "...";
    }

    /// <summary>
    /// zh-CN: 跟踪一次运行的根事件和工具调用事件映射，供后续事件建立正确父子关系。
    /// en: Tracks the root event and tool-call event mappings for one run so later events can attach to the correct parent nodes.
    /// </summary>
    private sealed class ProjectionState
    {
        public Guid? RootEventId { get; private set; }
        public Dictionary<string, Guid> ToolEvents { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// zh-CN: 根据新持久化的事件更新根节点或工具节点映射；遇到新的 RunStarted 时会重置旧的工具上下文。
        /// en: Updates root or tool mappings after each persisted event and resets stale tool context when a new RunStarted event appears.
        /// </summary>
        public void Apply(AgentEvent projectedEvent, string? toolCallId)
        {
            if (projectedEvent.EventType == EventTypes.RunStarted)
            {
                RootEventId = projectedEvent.Id;
                ToolEvents.Clear();
                return;
            }

            if (projectedEvent.EventType == EventTypes.ToolCall
                && !string.IsNullOrWhiteSpace(toolCallId))
            {
                // zh-CN: 记录 toolCallId 与持久化事件 ID 的映射，便于后续 ToolResult/ToolError 正确挂接父节点。
                // en: Record the mapping from toolCallId to persisted event ID so later ToolResult/ToolError events can attach to the correct parent.
                ToolEvents[toolCallId] = projectedEvent.Id;
            }
        }
    }
}
