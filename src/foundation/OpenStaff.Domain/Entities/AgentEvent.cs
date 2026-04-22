using System.Text.Json;

namespace OpenStaff.Entities;

/// <summary>
/// 智能体事件/消息 / Agent event
/// </summary>
public class AgentEvent:EntityBase<Guid>
{
    /// <summary>所属项目标识 / Owning project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>关联的项目内角色关联标识 / Related project-scoped role membership identifier.</summary>
    public Guid? ProjectAgentRoleId { get; set; }

    /// <summary>事件类型，例如 message、thought 或 tool_call / Event type, such as message, thought, or tool_call.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>事件主体内容 / Main event content.</summary>
    public string? Content { get; set; }

    /// <summary>附加元数据 JSON，例如 token 用量或耗时 / Additional metadata JSON, for example token usage or timings.</summary>
    public string? Metadata { get; set; }

    /// <summary>父事件标识，用于构建事件链 / Parent event identifier used to build an event chain.</summary>
    public Guid? ParentEventId { get; set; }

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>所属项目 / Owning project.</summary>
    public Project? Project { get; set; }

    /// <summary>关联的项目内角色关联 / Related project-scoped role membership.</summary>
    public ProjectAgentRole? ProjectAgentRole { get; set; }

    /// <summary>父事件 / Parent event in the chain.</summary>
    public AgentEvent? ParentEvent { get; set; }

    /// <summary>子事件集合 / Child events linked to this event.</summary>
    public ICollection<AgentEvent> ChildEvents { get; set; } = new List<AgentEvent>();
}

/// <summary>
/// 事件类型常量 / Well-known agent event type constants.
/// </summary>
public static class EventTypes
{
    public const string Message = "message"; // 角色间消息
    public const string Thought = "thought"; // 思考过程
    public const string Decision = "decision"; // 决策
    public const string Action = "action"; // 执行操作
    public const string Error = "error"; // 错误
    public const string Checkpoint = "checkpoint"; // 存储点事件
    public const string UserInput = "user_input"; // 用户输入
    public const string SystemNotice = "system_notice"; // 系统通知
    public const string TaskAssigned = "task_assigned"; // 任务分配
    public const string TaskQueued = "task_queued"; // 任务排队
    public const string TaskStarted = "task_started"; // 任务开始
    public const string TaskCompleted = "task_completed"; // 任务完成
    public const string TaskFailed = "task_failed"; // 任务失败
    public const string TaskRetry = "task_retry"; // 任务重试
    public const string CapabilityRequested = "capability_requested"; // 申请能力/工具
    public const string CapabilityApproved = "capability_approved"; // 能力申请已批准
    public const string RunStarted = "run_started"; // 运行开始
    public const string RunRetryScheduled = "run_retry_scheduled"; // 运行时自动重试
    public const string RunCancelled = "run_cancelled"; // 运行取消
    public const string ToolCall = "tool_call"; // 工具调用
    public const string ToolResult = "tool_result"; // 工具调用结果
    public const string ToolError = "tool_error"; // 工具调用错误
}

/// <summary>
/// 事件元数据载荷 / Structured metadata payload stored in <see cref="AgentEvent.Metadata"/>.
/// </summary>
public sealed class AgentEventMetadataPayload
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>关联任务标识 / Related task identifier.</summary>
    public Guid? TaskId { get; set; }

    /// <summary>关联会话标识 / Related session identifier.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>关联帧标识 / Related frame identifier.</summary>
    public Guid? FrameId { get; set; }

    /// <summary>关联消息标识 / Related message identifier.</summary>
    public Guid? MessageId { get; set; }

    /// <summary>关联执行包标识 / Related execution-package identifier.</summary>
    public Guid? ExecutionPackageId { get; set; }

    /// <summary>运行场景 / Execution scene.</summary>
    public string? Scene { get; set; }

    /// <summary>入口类型 / Business entry kind.</summary>
    public string? EntryKind { get; set; }

    /// <summary>当前角色标识 / Current role identifier.</summary>
    public Guid? AgentRoleId { get; set; }

    /// <summary>当前项目内角色关联标识 / Current project-scoped role membership identifier.</summary>
    public Guid? ProjectAgentRoleId { get; set; }

    /// <summary>目标角色标识 / Target role identifier.</summary>
    public Guid? TargetAgentRoleId { get; set; }

    /// <summary>目标项目内角色关联标识 / Target project-scoped role membership identifier.</summary>
    public Guid? TargetProjectAgentRoleId { get; set; }

    /// <summary>使用的模型名称 / Model name used during the event.</summary>
    public string? Model { get; set; }

    /// <summary>工具名称 / Tool name.</summary>
    public string? ToolName { get; set; }

    /// <summary>工具调用标识 / Tool call identifier.</summary>
    public string? ToolCallId { get; set; }

    /// <summary>状态快照 / Status snapshot.</summary>
    public string? Status { get; set; }

    /// <summary>来源帧标识 / Source frame identifier.</summary>
    public Guid? SourceFrameId { get; set; }

    /// <summary>来源 effect 序号 / Source effect index inside the execution package.</summary>
    public int? SourceEffectIndex { get; set; }

    /// <summary>来源信息 / Source description.</summary>
    public string? Source { get; set; }

    /// <summary>详细说明 / Additional detail.</summary>
    public string? Detail { get; set; }

    /// <summary>当前尝试次数 / Current attempt number.</summary>
    public int? Attempt { get; set; }

    /// <summary>允许的最大尝试次数 / Maximum retry attempts allowed.</summary>
    public int? MaxAttempts { get; set; }

    /// <summary>输入 Token 数 / Input token count.</summary>
    public int? InputTokens { get; set; }

    /// <summary>输出 Token 数 / Output token count.</summary>
    public int? OutputTokens { get; set; }

    /// <summary>总 Token 数 / Total token count.</summary>
    public int? TotalTokens { get; set; }

    /// <summary>总耗时（毫秒） / Total duration in milliseconds.</summary>
    public long? DurationMs { get; set; }

    /// <summary>首个 Token 延迟（毫秒） / First-token latency in milliseconds.</summary>
    public long? FirstTokenMs { get; set; }

    /// <summary>
    /// 尝试解析元数据 JSON / Try to parse metadata JSON into a typed payload without surfacing JSON format exceptions to callers.
    /// </summary>
    /// <param name="metadata">元数据 JSON / Metadata JSON.</param>
    /// <returns>解析结果；输入为空或格式非法时返回 <c>null</c> / Parsed payload, or <c>null</c> when the input is empty or invalid.</returns>
    public static AgentEventMetadataPayload? TryParse(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
            return null;

        try
        {
            return JsonSerializer.Deserialize<AgentEventMetadataPayload>(metadata, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
