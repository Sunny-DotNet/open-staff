namespace OpenStaff.Entities;

/// <summary>
/// 对话消息 — 帧内的一条消息
/// </summary>
public class ChatMessage:EntityBase<Guid>
{
    /// <summary>所属帧标识 / Owning frame identifier.</summary>
    public Guid FrameId { get; set; }

    /// <summary>所属会话标识 / Owning session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>所属执行包标识 / Owning execution-package identifier.</summary>
    public Guid? ExecutionPackageId { get; set; }

    /// <summary>来源帧标识 / Originating frame identifier.</summary>
    public Guid? OriginatingFrameId { get; set; }

    /// <summary>父消息标识 / Parent message identifier.</summary>
    public Guid? ParentMessageId { get; set; }

    /// <summary>消息角色：user / assistant / system / tool</summary>
    public string Role { get; set; } = "user";

    /// <summary>生成该消息的角色标识 / Agent-role identifier that produced this message.</summary>
    public Guid? AgentRoleId { get; set; }

    /// <summary>生成该消息的项目内角色关联标识 / Project-scoped role membership identifier that produced this message.</summary>
    public Guid? ProjectAgentRoleId { get; set; }

    /// <summary>消息内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>内容类型：text / markdown / json / image / internal</summary>
    public string ContentType { get; set; } = "text";

    /// <summary>帧内排序号</summary>
    public int SequenceNo { get; set; }

    /// <summary>Token 用量（JSON）</summary>
    public string? TokenUsage { get; set; }

    /// <summary>处理耗时（毫秒）</summary>
    public long? DurationMs { get; set; }

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>所属帧 / Owning frame.</summary>
    public ChatFrame? Frame { get; set; }

    /// <summary>所属会话 / Owning session.</summary>
    public ChatSession? Session { get; set; }

    /// <summary>所属执行包 / Owning execution package.</summary>
    public ExecutionPackage? ExecutionPackage { get; set; }

    /// <summary>来源帧 / Originating frame.</summary>
    public ChatFrame? OriginatingFrame { get; set; }

    /// <summary>父消息 / Parent message.</summary>
    public ChatMessage? ParentMessage { get; set; }

    /// <summary>子消息集合 / Child messages nested under this message.</summary>
    public ICollection<ChatMessage> ChildMessages { get; set; } = new List<ChatMessage>();
}

/// <summary>
/// 消息角色常量 / Well-known chat message roles.
/// </summary>
public static class MessageRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string System = "system";
    public const string Tool = "tool";
}

/// <summary>
/// 消息内容类型常量 / Supported chat message content types.
/// </summary>
public static class MessageContentTypes
{
    public const string Text = "text";
    public const string Markdown = "markdown";
    public const string Json = "json";
    public const string Image = "image";
    public const string Internal = "internal";
}
