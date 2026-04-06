namespace OpenStaff.Core.Models;

/// <summary>
/// 会话事件 — ReplaySubject 里的事件，也持久化到数据库
/// 用于回放整个会话过程
/// </summary>
public class SessionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid? FrameId { get; set; }

    /// <summary>事件类型</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>事件载荷（JSON）</summary>
    public string? Payload { get; set; }

    /// <summary>全局排序号（用于回放顺序）</summary>
    public long SequenceNo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ChatSession? Session { get; set; }
    public ChatFrame? Frame { get; set; }
}

/// <summary>
/// 会话事件类型
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
    public const string Error = "error";

    // 路由
    public const string Routing = "routing";

    // 流式输出
    public const string StreamingToken = "streaming_token";
    public const string StreamingDone = "streaming_done";

    // 用户交互
    public const string UserInput = "user_input";
    public const string AwaitingInput = "awaiting_input";
    public const string ResumedByUser = "resumed_by_user";
}
