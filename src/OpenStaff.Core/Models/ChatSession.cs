namespace OpenStaff.Core.Models;

/// <summary>
/// 对话会话 — 用户发起的一次完整交互
/// </summary>
public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }

    /// <summary>会话状态</summary>
    public string Status { get; set; } = SessionStatus.Active;

    /// <summary>用户的原始输入</summary>
    public string InitialInput { get; set; } = string.Empty;

    /// <summary>最终结果摘要</summary>
    public string? FinalResult { get; set; }

    /// <summary>上下文传递策略</summary>
    public string ContextStrategy { get; set; } = ContextStrategies.Full;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public Project? Project { get; set; }
    public ICollection<ChatFrame> Frames { get; set; } = new List<ChatFrame>();
    public ICollection<SessionEvent> Events { get; set; } = new List<SessionEvent>();
}

public static class SessionStatus
{
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Error = "error";
    /// <summary>等待用户输入（暂停链式流转）</summary>
    public const string AwaitingInput = "awaiting_input";
}

public static class ContextStrategies
{
    /// <summary>完整传递所有父 Frame 消息</summary>
    public const string Full = "full";
    /// <summary>只传父 Frame 摘要</summary>
    public const string Summary = "summary";
    /// <summary>当前帧完整 + 祖先帧摘要</summary>
    public const string Hybrid = "hybrid";
}
