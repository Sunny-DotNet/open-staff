namespace OpenStaff.Entities;

/// <summary>
/// 项目内角色关联 / Project-scoped agent-role membership
/// </summary>
public class ProjectAgentRole:EntityBase<Guid>
{
    /// <summary>所属项目标识 / Owning project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>角色定义标识 / Agent role identifier.</summary>
    public Guid AgentRoleId { get; set; }

    /// <summary>当前运行状态 / Current runtime status.</summary>
    public string Status { get; set; } = AgentStatus.Idle;

    /// <summary>当前任务描述 / Human-readable description of the current work item.</summary>
    public string? CurrentTask { get; set; }

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间（UTC） / Last update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>所属项目 / Owning project.</summary>
    public Project? Project { get; set; }

    /// <summary>关联角色定义 / Related agent role.</summary>
    public AgentRole? AgentRole { get; set; }

    /// <summary>项目内事件集合 / Events emitted by this project-scoped role membership.</summary>
    public ICollection<AgentEvent> Events { get; set; } = new List<AgentEvent>();

    /// <summary>已分配任务集合 / Tasks assigned to this project-scoped role membership.</summary>
    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();

    /// <summary>已绑定的 Skill 集合 / Skill bindings assigned to this project-scoped role membership.</summary>
    public ICollection<ProjectAgentRoleSkillBinding> SkillBindings { get; set; } = new List<ProjectAgentRoleSkillBinding>();
}

/// <summary>
/// 智能体状态常量 / Well-known project-agent statuses.
/// </summary>
public static class AgentStatus
{
    public const string Idle = "idle";
    public const string Thinking = "thinking";
    public const string Working = "working";
    public const string Paused = "paused";
    public const string Error = "error";
}
