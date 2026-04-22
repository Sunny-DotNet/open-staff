namespace OpenStaff.Entities;

/// <summary>
/// 对话栈帧 — 栈模式中的一个处理层级
/// </summary>
public class ChatFrame:EntityBase<Guid>
{
    /// <summary>所属会话标识 / Owning session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>所属执行包标识 / Owning execution-package identifier.</summary>
    public Guid? ExecutionPackageId { get; set; }

    /// <summary>父帧标识 / Parent frame identifier.</summary>
    public Guid? ParentFrameId { get; set; }

    /// <summary>关联任务标识 / Related task identifier.</summary>
    public Guid? TaskId { get; set; }

    /// <summary>栈深度（0 = 顶层用户交互）</summary>
    public int Depth { get; set; }

    /// <summary>发起者角色标识；为空表示用户 / Initiator role identifier; null means user.</summary>
    public Guid? InitiatorAgentRoleId { get; set; }

    /// <summary>发起者项目内角色关联标识；为空表示非项目角色或用户 / Initiator project-scoped role membership identifier; null means a non-project role or user.</summary>
    public Guid? InitiatorProjectAgentRoleId { get; set; }

    /// <summary>目标处理角色标识 / Target role identifier.</summary>
    public Guid? TargetAgentRoleId { get; set; }

    /// <summary>目标项目内角色关联标识 / Target project-scoped role membership identifier.</summary>
    public Guid? TargetProjectAgentRoleId { get; set; }

    /// <summary>本帧要解决的问题</summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>帧状态</summary>
    public string Status { get; set; } = FrameStatus.Active;

    /// <summary>完成后的结果</summary>
    public string? Result { get; set; }

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>完成时间（UTC） / Completion timestamp in UTC.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>所属会话 / Owning session.</summary>
    public ChatSession? Session { get; set; }

    /// <summary>所属执行包 / Owning execution package.</summary>
    public ExecutionPackage? ExecutionPackage { get; set; }

    /// <summary>父帧导航属性 / Parent frame navigation property.</summary>
    public ChatFrame? ParentFrame { get; set; }

    /// <summary>关联任务 / Related task.</summary>
    public TaskItem? Task { get; set; }

    /// <summary>子帧集合 / Child frames pushed from this frame.</summary>
    public ICollection<ChatFrame> ChildFrames { get; set; } = new List<ChatFrame>();

    /// <summary>帧内消息集合 / Messages produced within the frame.</summary>
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

/// <summary>
/// 帧状态常量 / Well-known chat frame states.
/// </summary>
public static class FrameStatus
{
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Popped = "popped";
    public const string Cancelled = "cancelled";
}
