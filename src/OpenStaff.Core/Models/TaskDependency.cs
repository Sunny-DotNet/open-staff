namespace OpenStaff.Core.Models;

/// <summary>
/// 任务依赖关系 / Task dependency
/// </summary>
public class TaskDependency
{
    public Guid TaskId { get; set; }
    public Guid DependsOnId { get; set; }

    public TaskItem? Task { get; set; }
    public TaskItem? DependsOn { get; set; }
}
