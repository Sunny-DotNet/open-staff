namespace OpenStaff.Entities;

/// <summary>
/// 一次完整执行单元 / A single end-to-end execution unit within a conversation session.
/// </summary>
public class ExecutionPackage : EntityBase<Guid>
{
    /// <summary>所属项目标识 / Owning project identifier.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>所属会话标识 / Owning session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>父执行包标识 / Parent execution-package identifier.</summary>
    public Guid? ParentExecutionPackageId { get; set; }

    /// <summary>重试来源执行包标识 / Original execution package retried by this package.</summary>
    public Guid? RetryOfExecutionPackageId { get; set; }

    /// <summary>来源帧标识 / Source frame that spawned this package.</summary>
    public Guid? SourceFrameId { get; set; }

    /// <summary>根帧标识 / Root frame enclosed by this package.</summary>
    public Guid? RootFrameId { get; set; }

    /// <summary>关联任务标识 / Related task identifier.</summary>
    public Guid? TaskId { get; set; }

    /// <summary>入口类型 / Business entry kind.</summary>
    public string EntryKind { get; set; } = ExecutionEntryKinds.ProjectBrainstorm;

    /// <summary>执行包类型 / Package kind.</summary>
    public string PackageKind { get; set; } = ExecutionPackageKinds.Entry;

    /// <summary>场景名称 / Scene name.</summary>
    public string Scene { get; set; } = SessionSceneTypes.ProjectBrainstorm;

    /// <summary>执行状态 / Current execution status.</summary>
    public string Status { get; set; } = ExecutionPackageStatus.Active;

    /// <summary>本次输入的摘要文本 / Input summary for this package.</summary>
    public string? InputSummary { get; set; }

    /// <summary>发起角色标识；为空表示用户 / Initiator role identifier; null means user.</summary>
    public Guid? InitiatorAgentRoleId { get; set; }

    /// <summary>发起项目内角色关联标识；为空表示非项目角色或用户 / Initiator project-scoped role membership identifier; null means a non-project role or user.</summary>
    public Guid? InitiatorProjectAgentRoleId { get; set; }

    /// <summary>目标角色标识 / Target role identifier.</summary>
    public Guid? TargetAgentRoleId { get; set; }

    /// <summary>目标项目内角色关联标识 / Target project-scoped role membership identifier.</summary>
    public Guid? TargetProjectAgentRoleId { get; set; }

    /// <summary>显式角色定义标识 / Explicit agent-role identifier when applicable.</summary>
    public Guid? AgentRoleId { get; set; }

    /// <summary>显式项目内角色关联标识 / Explicit project-scoped role membership identifier when applicable.</summary>
    public Guid? ProjectAgentRoleId { get; set; }

    /// <summary>执行效果序列 JSON / Serialized effect list JSON.</summary>
    public string? EffectsJson { get; set; }

    /// <summary>完整快照路径 / Optional snapshot path for persisted packages.</summary>
    public string? SnapshotPath { get; set; }

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间（UTC） / Last update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>完成时间（UTC） / Completion timestamp in UTC.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>所属会话 / Owning session.</summary>
    public ChatSession? Session { get; set; }

    /// <summary>关联任务 / Related task.</summary>
    public TaskItem? Task { get; set; }

    /// <summary>根帧 / Root frame.</summary>
    public ChatFrame? RootFrame { get; set; }

    /// <summary>来源帧 / Source frame.</summary>
    public ChatFrame? SourceFrame { get; set; }

    /// <summary>父执行包 / Parent execution package.</summary>
    public ExecutionPackage? ParentExecutionPackage { get; set; }

    /// <summary>被重试的执行包 / Original package retried by this package.</summary>
    public ExecutionPackage? RetryOfExecutionPackage { get; set; }

    /// <summary>子执行包集合 / Child execution packages.</summary>
    public ICollection<ExecutionPackage> ChildExecutionPackages { get; set; } = new List<ExecutionPackage>();

    /// <summary>重试执行包集合 / Retry packages created from this package.</summary>
    public ICollection<ExecutionPackage> RetryExecutionPackages { get; set; } = new List<ExecutionPackage>();

    /// <summary>包内帧集合 / Frames enclosed by this package.</summary>
    public ICollection<ChatFrame> Frames { get; set; } = new List<ChatFrame>();

    /// <summary>关联消息集合 / Messages projected from this package.</summary>
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

    /// <summary>关联事件集合 / Session events projected from this package.</summary>
    public ICollection<SessionEvent> Events { get; set; } = new List<SessionEvent>();

    /// <summary>任务执行链接集合 / Task projection links emitted by this package.</summary>
    public ICollection<TaskExecutionLink> TaskExecutionLinks { get; set; } = new List<TaskExecutionLink>();
}

/// <summary>
/// 执行包状态常量 / Well-known execution-package states.
/// </summary>
public static class ExecutionPackageStatus
{
    public const string Active = "active";
    public const string AwaitingInput = "awaiting_input";
    public const string Queued = "queued";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

/// <summary>
/// 执行包类型常量 / Well-known execution-package kinds.
/// </summary>
public static class ExecutionPackageKinds
{
    public const string Entry = "entry";
    public const string Retry = "retry";
    public const string Child = "child";
}

/// <summary>
/// 统一入口类型常量 / Stable entry-kind values persisted with execution packages.
/// </summary>
public static class ExecutionEntryKinds
{
    public const string TestChat = "test_chat";
    public const string ProjectBrainstorm = "project_brainstorm";
    public const string ProjectGroup = "project_group";
    public const string ProjectAgentPrivate = "project_agent_private";
    public const string SessionReply = "session_reply";
}

/// <summary>
/// 任务与执行包之间的投影索引 / Query-oriented projection link between a task and an execution package.
/// </summary>
public class TaskExecutionLink : EntityBase<Guid>
{
    /// <summary>关联任务标识 / Related task identifier.</summary>
    public Guid TaskId { get; set; }

    /// <summary>关联执行包标识 / Related execution-package identifier.</summary>
    public Guid ExecutionPackageId { get; set; }

    /// <summary>动作类型 / Projected action kind.</summary>
    public string Action { get; set; } = TaskExecutionActions.Commented;

    /// <summary>来源 effect 序号 / Source effect index inside the execution package.</summary>
    public int? SourceEffectIndex { get; set; }

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>关联任务 / Related task.</summary>
    public TaskItem? Task { get; set; }

    /// <summary>关联执行包 / Related execution package.</summary>
    public ExecutionPackage? ExecutionPackage { get; set; }
}

/// <summary>
/// 任务执行投影动作常量 / Shared action vocabulary for task execution projections.
/// </summary>
public static class TaskExecutionActions
{
    public const string Created = "created";
    public const string Assigned = "assigned";
    public const string StatusChanged = "status_changed";
    public const string Commented = "commented";
    public const string RetriedFor = "retried_for";
}
