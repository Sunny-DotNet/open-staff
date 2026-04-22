namespace OpenStaff.Entities;

/// <summary>
/// 代码存储点 / Code checkpoint
/// </summary>
public class Checkpoint:EntityBase<Guid>
{
    /// <summary>所属项目标识 / Owning project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>关联任务标识 / Related task identifier.</summary>
    public Guid? TaskId { get; set; }

    /// <summary>关联项目内角色关联标识 / Related project-scoped role membership identifier.</summary>
    public Guid? ProjectAgentRoleId { get; set; }

    /// <summary>对应的 Git 提交 SHA / Git commit SHA captured by the checkpoint.</summary>
    public string? GitCommitSha { get; set; }

    /// <summary>检查点描述 / Human-readable checkpoint description.</summary>
    public string? Description { get; set; }

    /// <summary>差异摘要 / Summary of the recorded diff.</summary>
    public string? DiffSummary { get; set; }

    /// <summary>变更文件列表 JSON / JSON list of changed files.</summary>
    public string? FilesChanged { get; set; }

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>所属项目 / Owning project.</summary>
    public Project? Project { get; set; }

    /// <summary>关联任务 / Related task.</summary>
    public TaskItem? Task { get; set; }

    /// <summary>关联项目内角色关联 / Related project-scoped role membership.</summary>
    public ProjectAgentRole? ProjectAgentRole { get; set; }
}
