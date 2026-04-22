namespace OpenStaff.Entities;

/// <summary>
/// 任务依赖关系 / Task dependency
/// </summary>
public class TaskDependency : EntityBase<Guid>
{
    /// <summary>任务标识 / Dependent task identifier.</summary>
    public Guid TaskId { get; set; }

    /// <summary>前置任务标识 / Prerequisite task identifier.</summary>
    public Guid DependsOnId { get; set; }

    /// <summary>依赖方任务 / Dependent task.</summary>
    public TaskItem? Task { get; set; }

    /// <summary>被依赖的前置任务 / Prerequisite task.</summary>
    public TaskItem? DependsOn { get; set; }
}
