namespace OpenStaff.Core.Models;

/// <summary>
/// 对话栈帧 — 栈模式中的一个处理层级
/// </summary>
public class ChatFrame
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid? ParentFrameId { get; set; }

    /// <summary>栈深度（0 = 顶层用户交互）</summary>
    public int Depth { get; set; }

    /// <summary>发起者角色（user 或 Agent 角色类型）</summary>
    public string InitiatorRole { get; set; } = "user";

    /// <summary>目标处理角色</summary>
    public string? TargetRole { get; set; }

    /// <summary>本帧要解决的问题</summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>帧状态</summary>
    public string Status { get; set; } = FrameStatus.Active;

    /// <summary>完成后的结果</summary>
    public string? Result { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public ChatSession? Session { get; set; }
    public ChatFrame? ParentFrame { get; set; }
    public ICollection<ChatFrame> ChildFrames { get; set; } = new List<ChatFrame>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public static class FrameStatus
{
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Popped = "popped";
    public const string Cancelled = "cancelled";
}
