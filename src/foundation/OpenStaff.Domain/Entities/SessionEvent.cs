namespace OpenStaff.Entities;

/// <summary>
/// 会话事件 — ReplaySubject 里的事件，也持久化到数据库
/// 用于回放整个会话过程
/// </summary>
public class SessionEvent:EntityBase<Guid>
{
    /// <summary>所属会话标识 / Owning session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>所属执行包标识 / Owning execution-package identifier.</summary>
    public Guid? ExecutionPackageId { get; set; }

    /// <summary>关联帧标识 / Related frame identifier.</summary>
    public Guid? FrameId { get; set; }

    /// <summary>关联消息标识 / Related message identifier.</summary>
    public Guid? MessageId { get; set; }

    /// <summary>来源帧标识 / Source frame identifier.</summary>
    public Guid? SourceFrameId { get; set; }

    /// <summary>来源 effect 序号 / Source effect index inside the execution package.</summary>
    public int? SourceEffectIndex { get; set; }

    /// <summary>事件类型</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>事件载荷（JSON）</summary>
    public string? Payload { get; set; }

    /// <summary>全局排序号（用于回放顺序）</summary>
    public long SequenceNo { get; set; }

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>所属会话 / Owning session.</summary>
    public ChatSession? Session { get; set; }

    /// <summary>所属执行包 / Owning execution package.</summary>
    public ExecutionPackage? ExecutionPackage { get; set; }

    /// <summary>关联帧 / Related frame.</summary>
    public ChatFrame? Frame { get; set; }

    /// <summary>关联消息 / Related message.</summary>
    public ChatMessage? Message { get; set; }
}

/// <summary>
/// 会话事件类型 / Well-known session event type constants.
/// </summary>
public static class SessionEventTypes
{
    // 会话生命周期
    public const string SessionCreated = "session_created";
    public const string SessionCompleted = "session_completed";
    public const string SessionCancelled = "session_cancelled";
    public const string SessionError = "session_error";

    // 帧生命周期
    public const string FramePushed = "frame_pushed";
    public const string FrameCompleted = "frame_completed";
    public const string FramePopped = "frame_popped";

    // Agent 处理过程
    public const string Thought = "thought";
    public const string Decision = "decision";
    public const string Message = "message";
    public const string Action = "action";
    public const string ToolCall = "tool_call";
    public const string ToolResult = "tool_result";
    public const string ToolError = "tool_error";
    public const string Error = "error";

    // 路由
    public const string Routing = "routing";

    // 流式输出
    public const string StreamingToken = "streaming_token";
    public const string StreamingThinking = "streaming_thinking";
    public const string StreamingDone = "streaming_done";

    // 用户交互
    public const string UserInput = "user_input";
    public const string AwaitingInput = "awaiting_input";
    public const string ResumedByUser = "resumed_by_user";

    // 项目场景状态
    public const string ProjectStateChanged = "project_state_changed";
    public const string TaskStateChanged = "task_state_changed";
    public const string AgentStatusChanged = "agent_status_changed";
}
