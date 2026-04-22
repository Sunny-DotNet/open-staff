namespace OpenStaff.Entities;

/// <summary>
/// 工程模型 / Project entity
/// </summary>
public class Project:EntityBase<Guid>
{
    /// <summary>项目名称 / Project name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>项目描述 / Optional project description.</summary>
    public string? Description { get; set; }

    /// <summary>交互语言代码 / Language code used for project interactions.</summary>
    public string Language { get; set; } = "zh-CN";

    /// <summary>项目工作空间路径 / Workspace path for generated files and execution.</summary>
    public string? WorkspacePath { get; set; }

    /// <summary>项目生命周期状态 / High-level project lifecycle status.</summary>
    public string Status { get; set; } = ProjectStatus.Initializing;

    /// <summary>项目执行阶段 / Current delivery phase of the project.</summary>
    public string Phase { get; set; } = ProjectPhases.Brainstorming;

    /// <summary>额外元数据 JSON / Additional metadata stored as JSON.</summary>
    public string? Metadata { get; set; }

    /// <summary>项目备用模型供应商 ID</summary>
    public Guid? DefaultProviderId { get; set; }
    /// <summary>项目备用模型名称</summary>
    public string? DefaultModelName { get; set; }
    /// <summary>扩展参数 (JSON 键值对，用于存储环境变量等)</summary>
    public string? ExtraConfig { get; set; }

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间（UTC） / Last update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>项目中的角色关联 / Project-scoped role memberships.</summary>
    public ICollection<ProjectAgentRole> AgentRoles { get; set; } = new List<ProjectAgentRole>();

    /// <summary>项目任务集合 / Tasks that belong to the project.</summary>
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

    /// <summary>项目事件流 / Agent events linked to the project.</summary>
    public ICollection<AgentEvent> Events { get; set; } = new List<AgentEvent>();

    /// <summary>代码检查点集合 / Code checkpoints recorded for the project.</summary>
    public ICollection<Checkpoint> Checkpoints { get; set; } = new List<Checkpoint>();

    /// <summary>项目作用域的 Skill 安装记录。 / Project-scoped installed skills.</summary>
    public ICollection<InstalledSkill> InstalledSkills { get; set; } = new List<InstalledSkill>();
}

/// <summary>
/// 项目状态常量 / Well-known project lifecycle states.
/// </summary>
public static class ProjectStatus
{
    public const string Initializing = "initializing";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Completed = "completed";
    public const string Archived = "archived";
}

/// <summary>
/// 项目阶段常量 / Well-known project delivery phases.
/// </summary>
public static class ProjectPhases
{
    public const string Brainstorming = "brainstorming";
    public const string ReadyToStart = "ready_to_start";
    public const string Running = "running";
    public const string Completed = "completed";
}
