namespace OpenStaff.Core.Models;

/// <summary>
/// 任务项 / Task item
/// </summary>
public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = TaskItemStatus.Pending;
    public int Priority { get; set; } = 0;
    public Guid? AssignedAgentId { get; set; }
    public Guid? ParentTaskId { get; set; } // 父任务(支持嵌套)
    public string? Metadata { get; set; } // JSON
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // 导航属性
    public Project? Project { get; set; }
    public ProjectAgent? AssignedAgent { get; set; }
    public TaskItem? ParentTask { get; set; }
    public ICollection<TaskItem> SubTasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskDependency> Dependencies { get; set; } = new List<TaskDependency>();
    public ICollection<TaskDependency> Dependents { get; set; } = new List<TaskDependency>();
}

public static class TaskItemStatus
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Done = "done";
    public const string Blocked = "blocked";
    public const string Cancelled = "cancelled";
}
