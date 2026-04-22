using System.Text.Json;

namespace OpenStaff.Entities;

/// <summary>
/// 任务项 / Task item
/// </summary>
public class TaskItem:EntityBase<Guid>
{
    /// <summary>所属项目标识 / Owning project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>任务标题 / Task title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>任务描述 / Optional task description.</summary>
    public string? Description { get; set; }

    /// <summary>任务状态 / Current task status.</summary>
    public string Status { get; set; } = TaskItemStatus.Pending;

    /// <summary>优先级，数值越大优先级越高 / Priority, where larger numbers mean higher priority.</summary>
    public int Priority { get; set; } = 0;

    /// <summary>已分配项目内角色关联标识 / Assigned project-scoped role membership identifier.</summary>
    public Guid? AssignedProjectAgentRoleId { get; set; }

    /// <summary>父任务标识（支持嵌套任务） / Parent task identifier for nested tasks.</summary>
    public Guid? ParentTaskId { get; set; }

    /// <summary>运行时元数据 JSON / Runtime metadata stored as JSON.</summary>
    public string? Metadata { get; set; }

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间（UTC） / Last update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>完成时间（UTC） / Completion timestamp in UTC.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>所属项目 / Owning project.</summary>
    public Project? Project { get; set; }

    /// <summary>分配的项目内角色关联 / Assigned project-scoped role membership.</summary>
    public ProjectAgentRole? AssignedProjectAgentRole { get; set; }

    /// <summary>父任务 / Parent task.</summary>
    public TaskItem? ParentTask { get; set; }

    /// <summary>子任务集合 / Nested child tasks.</summary>
    public ICollection<TaskItem> SubTasks { get; set; } = new List<TaskItem>();

    /// <summary>前置依赖集合 / Dependencies required before this task can run.</summary>
    public ICollection<TaskDependency> Dependencies { get; set; } = new List<TaskDependency>();

    /// <summary>后继依赖集合 / Dependents waiting on this task.</summary>
    public ICollection<TaskDependency> Dependents { get; set; } = new List<TaskDependency>();

    /// <summary>与任务关联的对话帧 / Chat frames linked to the task.</summary>
    public ICollection<ChatFrame> Frames { get; set; } = new List<ChatFrame>();

    /// <summary>与任务关联的执行包索引 / Execution-package links associated with the task.</summary>
    public ICollection<TaskExecutionLink> ExecutionLinks { get; set; } = new List<TaskExecutionLink>();
}

/// <summary>
/// 任务状态常量 / Well-known task statuses.
/// </summary>
public static class TaskItemStatus
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Done = "done";
    public const string Blocked = "blocked";
    public const string Cancelled = "cancelled";
}

/// <summary>
/// 任务运行时元数据 / Structured runtime metadata stored in <see cref="TaskItem.Metadata"/>.
/// </summary>
public sealed class TaskItemRuntimeMetadata
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>默认最大尝试次数 / Default maximum retry attempts.</summary>
    public const int MaxAttempts = 3;

    /// <summary>关联会话标识 / Related session identifier.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>关联执行包标识 / Related execution-package identifier.</summary>
    public Guid? ExecutionPackageId { get; set; }

    /// <summary>关联帧标识 / Related frame identifier.</summary>
    public Guid? FrameId { get; set; }

    /// <summary>关联消息标识 / Related message identifier.</summary>
    public Guid? MessageId { get; set; }

    /// <summary>运行场景 / Execution scene.</summary>
    public string? Scene { get; set; }

    /// <summary>入口类型 / Entry kind that produced the current runtime metadata.</summary>
    public string? EntryKind { get; set; }

    /// <summary>用户显式提及的角色标识 / User-mentioned role identifier.</summary>
    public Guid? MentionedAgentRoleId { get; set; }

    /// <summary>用户显式提及的项目内角色关联标识 / User-mentioned project-scoped role membership identifier.</summary>
    public Guid? MentionedProjectAgentRoleId { get; set; }

    /// <summary>来源信息 / Source description.</summary>
    public string? Source { get; set; }

    /// <summary>目标角色标识 / Target role identifier.</summary>
    public Guid? TargetAgentRoleId { get; set; }

    /// <summary>目标项目内角色关联标识 / Target project-scoped role membership identifier.</summary>
    public Guid? TargetProjectAgentRoleId { get; set; }

    /// <summary>最近一次状态 / Most recent status.</summary>
    public string? LastStatus { get; set; }

    /// <summary>来源帧标识 / Source frame that produced the latest visible effect.</summary>
    public Guid? SourceFrameId { get; set; }

    /// <summary>来源 effect 序号 / Source effect index inside the latest execution package.</summary>
    public int? SourceEffectIndex { get; set; }

    /// <summary>最近一次结果摘要 / Most recent result summary.</summary>
    public string? LastResult { get; set; }

    /// <summary>最近一次错误信息 / Most recent error text.</summary>
    public string? LastError { get; set; }

    /// <summary>使用的模型名称 / Model name used for execution.</summary>
    public string? Model { get; set; }

    /// <summary>已尝试次数 / Number of attempts performed.</summary>
    public int AttemptCount { get; set; }

    /// <summary>总 Token 数 / Total token count.</summary>
    public int? TotalTokens { get; set; }

    /// <summary>总耗时（毫秒） / Total duration in milliseconds.</summary>
    public long? DurationMs { get; set; }

    /// <summary>首个 Token 延迟（毫秒） / First-token latency in milliseconds.</summary>
    public long? FirstTokenMs { get; set; }

    /// <summary>
    /// 尝试解析运行时元数据 JSON / Try to parse runtime metadata JSON into a typed payload without surfacing JSON format exceptions to callers.
    /// </summary>
    /// <param name="metadata">元数据 JSON / Metadata JSON.</param>
    /// <returns>解析结果；输入为空或格式非法时返回 <c>null</c> / Parsed payload, or <c>null</c> when the input is empty or invalid.</returns>
    public static TaskItemRuntimeMetadata? TryParse(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
            return null;

        try
        {
            return JsonSerializer.Deserialize<TaskItemRuntimeMetadata>(metadata, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
