namespace OpenStaff.Application.Contracts.Tasks.Dtos;

public class TaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public Guid? AssignedAgentId { get; set; }
    public string? AssignedAgentName { get; set; }
    public string? AssignedRoleName { get; set; }
    public Guid? ParentTaskId { get; set; }
    public List<TaskDto>? SubTasks { get; set; }
    public List<Guid>? Dependencies { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class CreateTaskInput
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Priority { get; set; }
    public Guid? ParentTaskId { get; set; }
    public Guid? AssignedAgentId { get; set; }
    public List<Guid>? DependsOn { get; set; }
}

public class UpdateTaskInput
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public int? Priority { get; set; }
    public Guid? AssignedAgentId { get; set; }
}

public class TaskStatusUpdateInput
{
    public Guid TaskId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class TaskTimelineDto
{
    public Guid Id { get; set; }
    public string? EventType { get; set; }
    public string? Data { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GetAllTasksRequest
{
    public Guid ProjectId { get; set; }
    public string? Status { get; set; }
}

public class GetTaskByIdRequest
{
    public Guid ProjectId { get; set; }
    public Guid TaskId { get; set; }
}

public class CreateTaskRequest
{
    public Guid ProjectId { get; set; }
    public CreateTaskInput Input { get; set; } = new();
}

public class UpdateTaskRequest
{
    public Guid ProjectId { get; set; }
    public Guid TaskId { get; set; }
    public UpdateTaskInput Input { get; set; } = new();
}

public class DeleteTaskRequest
{
    public Guid ProjectId { get; set; }
    public Guid TaskId { get; set; }
}

public class GetTaskTimelineRequest
{
    public Guid ProjectId { get; set; }
    public Guid TaskId { get; set; }
}

public class BatchUpdateTaskStatusRequest
{
    public Guid ProjectId { get; set; }
    public List<TaskStatusUpdateInput> Updates { get; set; } = [];
}
