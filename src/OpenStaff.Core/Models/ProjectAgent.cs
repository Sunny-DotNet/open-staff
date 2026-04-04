namespace OpenStaff.Core.Models;

/// <summary>
/// 工程-角色实例 / Project agent instance
/// </summary>
public class ProjectAgent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid AgentRoleId { get; set; }
    public string Status { get; set; } = AgentStatus.Idle;
    public string? CurrentTask { get; set; } // 当前正在做什么
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    public Project? Project { get; set; }
    public AgentRole? AgentRole { get; set; }
    public ICollection<AgentEvent> Events { get; set; } = new List<AgentEvent>();
    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
}

public static class AgentStatus
{
    public const string Idle = "idle";
    public const string Thinking = "thinking";
    public const string Working = "working";
    public const string Paused = "paused";
    public const string Error = "error";
}
