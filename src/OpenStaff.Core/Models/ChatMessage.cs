namespace OpenStaff.Core.Models;

/// <summary>
/// 对话消息 — 帧内的一条消息
/// </summary>
public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FrameId { get; set; }
    public Guid SessionId { get; set; }

    /// <summary>消息角色：user / assistant / system / tool</summary>
    public string Role { get; set; } = "user";

    /// <summary>具体 Agent 角色类型（如 architect, producer）</summary>
    public string? AgentRole { get; set; }

    /// <summary>消息内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>内容类型：text / markdown / json / image</summary>
    public string ContentType { get; set; } = "text";

    /// <summary>帧内排序号</summary>
    public int SequenceNo { get; set; }

    /// <summary>Token 用量（JSON）</summary>
    public string? TokenUsage { get; set; }

    /// <summary>处理耗时（毫秒）</summary>
    public long? DurationMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ChatFrame? Frame { get; set; }
    public ChatSession? Session { get; set; }
}

public static class MessageRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string System = "system";
    public const string Tool = "tool";
}
