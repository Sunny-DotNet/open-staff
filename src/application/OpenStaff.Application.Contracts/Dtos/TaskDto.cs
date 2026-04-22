namespace OpenStaff.Dtos;

/// <summary>
/// 任务摘要信息。
/// Summary information for a task.
/// </summary>
public class TaskDto
{
    /// <summary>任务唯一标识。 / Unique task identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>任务标题。 / Task title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>任务描述。 / Task description.</summary>
    public string? Description { get; set; }

    /// <summary>当前任务状态。 / Current task status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>优先级值，数值越小通常越优先。 / Priority value, where a smaller number is typically higher priority.</summary>
    public int Priority { get; set; }

    /// <summary>分配的项目内角色关联标识。 / Assigned project-scoped role membership identifier.</summary>
    public Guid? AssignedProjectAgentRoleId { get; set; }

    /// <summary>分配的智能体名称。 / Assigned agent name.</summary>
    public string? AssignedAgentName { get; set; }

    /// <summary>分配的角色名称。 / Assigned role name.</summary>
    public string? AssignedRoleName { get; set; }

    /// <summary>父任务标识。 / Parent task identifier.</summary>
    public Guid? ParentTaskId { get; set; }

    /// <summary>子任务列表。 / Child tasks.</summary>
    public List<TaskDto>? SubTasks { get; set; }

    /// <summary>依赖任务标识列表。 / Identifiers of tasks this task depends on.</summary>
    public List<Guid>? Dependencies { get; set; }

    /// <summary>关联会话标识。 / Associated session identifier.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>关联执行包标识。 / Associated execution-package identifier.</summary>
    public Guid? ExecutionPackageId { get; set; }

    /// <summary>关联帧标识。 / Associated frame identifier.</summary>
    public Guid? FrameId { get; set; }

    /// <summary>关联消息标识。 / Associated message identifier.</summary>
    public Guid? MessageId { get; set; }

    /// <summary>任务所在场景。 / Scene in which the task runs.</summary>
    public string? Scene { get; set; }

    /// <summary>入口类型。 / Entry kind that produced the latest task metadata.</summary>
    public string? EntryKind { get; set; }

    /// <summary>显式提及的角色标识。 / Explicitly mentioned role identifier from the user input.</summary>
    public Guid? MentionedAgentRoleId { get; set; }

    /// <summary>显式提及的项目内角色关联标识。 / Explicitly mentioned project-scoped role membership identifier from the user input.</summary>
    public Guid? MentionedProjectAgentRoleId { get; set; }

    /// <summary>分发来源。 / Dispatch source.</summary>
    public string? DispatchSource { get; set; }

    /// <summary>目标角色标识。 / Target role identifier expected to execute the task.</summary>
    public Guid? TargetAgentRoleId { get; set; }

    /// <summary>目标项目内角色关联标识。 / Target project-scoped role membership identifier expected to execute the task.</summary>
    public Guid? TargetProjectAgentRoleId { get; set; }

    /// <summary>最近一次运行的状态。 / Status recorded for the latest execution attempt.</summary>
    public string? LastStatus { get; set; }

    /// <summary>来源帧标识。 / Source frame identifier.</summary>
    public Guid? SourceFrameId { get; set; }

    /// <summary>来源 effect 序号。 / Source effect index.</summary>
    public int? SourceEffectIndex { get; set; }

    /// <summary>最近一次运行结果摘要。 / Result summary from the latest execution attempt.</summary>
    public string? LastResult { get; set; }

    /// <summary>最近一次运行错误信息。 / Error recorded for the latest execution attempt.</summary>
    public string? LastError { get; set; }

    /// <summary>最近使用的模型。 / Most recently used model.</summary>
    public string? Model { get; set; }

    /// <summary>已尝试次数。 / Number of attempts made so far.</summary>
    public int AttemptCount { get; set; }

    /// <summary>总 Token 数。 / Total token count.</summary>
    public int? TotalTokens { get; set; }

    /// <summary>总耗时（毫秒）。 / Total duration in milliseconds.</summary>
    public long? DurationMs { get; set; }

    /// <summary>首 Token 延迟（毫秒）。 / First-token latency in milliseconds.</summary>
    public long? FirstTokenMs { get; set; }

    /// <summary>创建时间（UTC）。 / Creation time in UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>最后更新时间（UTC）。 / Last update time in UTC.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>完成时间（UTC）。 / Completion time in UTC.</summary>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// 创建任务的输入参数。
/// Input used to create a task.
/// </summary>
public class CreateTaskInput
{
    /// <summary>任务标题。 / Task title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>任务描述。 / Task description.</summary>
    public string? Description { get; set; }

    /// <summary>优先级。 / Priority value.</summary>
    public int Priority { get; set; }

    /// <summary>父任务标识。 / Parent task identifier.</summary>
    public Guid? ParentTaskId { get; set; }

    /// <summary>预分配的项目内角色关联标识。 / Pre-assigned project-scoped role membership identifier.</summary>
    public Guid? AssignedProjectAgentRoleId { get; set; }

    /// <summary>依赖任务标识列表。 / Identifiers of prerequisite tasks.</summary>
    public List<Guid>? DependsOn { get; set; }
}

/// <summary>
/// 更新任务的输入参数。
/// Input used to update a task.
/// </summary>
public class UpdateTaskInput
{
    /// <summary>任务标题。 / Task title.</summary>
    public string? Title { get; set; }

    /// <summary>任务描述。 / Task description.</summary>
    public string? Description { get; set; }

    /// <summary>任务状态。 / Task status.</summary>
    public string? Status { get; set; }

    /// <summary>优先级。 / Priority value.</summary>
    public int? Priority { get; set; }

    /// <summary>分配的项目内角色关联标识。 / Assigned project-scoped role membership identifier.</summary>
    public Guid? AssignedProjectAgentRoleId { get; set; }
}

/// <summary>
/// 单个任务状态更新项。
/// Single task status update item.
/// </summary>
public class TaskStatusUpdateInput
{
    /// <summary>任务标识。 / Task identifier.</summary>
    public Guid TaskId { get; set; }

    /// <summary>新状态。 / New status value.</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// 任务运行时间线事件。
/// Timeline event emitted during task execution.
/// </summary>
public class TaskTimelineDto
{
    /// <summary>事件唯一标识。 / Unique event identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>事件类型。 / Event type.</summary>
    public string? EventType { get; set; }

    /// <summary>原始数据。 / Raw event data.</summary>
    public string? Data { get; set; }

    /// <summary>面向用户的内容文本。 / User-facing content text.</summary>
    public string? Content { get; set; }

    /// <summary>扩展元数据 JSON。 / Additional metadata JSON.</summary>
    public string? Metadata { get; set; }

    /// <summary>关联任务标识。 / Associated task identifier.</summary>
    public Guid? TaskId { get; set; }

    /// <summary>关联会话标识。 / Associated session identifier.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>关联执行包标识。 / Associated execution-package identifier.</summary>
    public Guid? ExecutionPackageId { get; set; }

    /// <summary>关联帧标识。 / Associated frame identifier.</summary>
    public Guid? FrameId { get; set; }

    /// <summary>关联消息标识。 / Associated message identifier.</summary>
    public Guid? MessageId { get; set; }

    /// <summary>事件所属场景。 / Scene in which the event was emitted.</summary>
    public string? Scene { get; set; }

    /// <summary>入口类型。 / Business entry kind.</summary>
    public string? EntryKind { get; set; }

    /// <summary>执行角色标识。 / Role identifier that executed the event.</summary>
    public Guid? AgentRoleId { get; set; }

    /// <summary>执行项目内角色关联标识。 / Project-scoped role membership identifier that executed the event.</summary>
    public Guid? ProjectAgentRoleId { get; set; }

    /// <summary>目标角色标识。 / Target role identifier.</summary>
    public Guid? TargetAgentRoleId { get; set; }

    /// <summary>目标项目内角色关联标识。 / Target project-scoped role membership identifier.</summary>
    public Guid? TargetProjectAgentRoleId { get; set; }

    /// <summary>使用的模型。 / Model used for the event.</summary>
    public string? Model { get; set; }

    /// <summary>调用的工具名称。 / Tool name used during the event.</summary>
    public string? ToolName { get; set; }

    /// <summary>工具调用标识。 / Tool call identifier.</summary>
    public string? ToolCallId { get; set; }

    /// <summary>事件状态。 / Event status.</summary>
    public string? Status { get; set; }

    /// <summary>来源帧标识。 / Source frame identifier.</summary>
    public Guid? SourceFrameId { get; set; }

    /// <summary>来源 effect 序号。 / Source effect index.</summary>
    public int? SourceEffectIndex { get; set; }

    /// <summary>补充说明。 / Additional detail.</summary>
    public string? Detail { get; set; }

    /// <summary>当前尝试次数。 / Current attempt count.</summary>
    public int? Attempt { get; set; }

    /// <summary>允许的最大尝试次数。 / Maximum allowed attempts.</summary>
    public int? MaxAttempts { get; set; }

    /// <summary>总 Token 数。 / Total token count.</summary>
    public int? TotalTokens { get; set; }

    /// <summary>总耗时（毫秒）。 / Total duration in milliseconds.</summary>
    public long? DurationMs { get; set; }

    /// <summary>首 Token 延迟（毫秒）。 / First-token latency in milliseconds.</summary>
    public long? FirstTokenMs { get; set; }

    /// <summary>创建时间（UTC）。 / Creation time in UTC.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 查询项目任务列表的请求。
/// Request used to list project tasks.
/// </summary>
public class GetAllTasksRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>可选的状态过滤条件。 / Optional status filter.</summary>
    public string? Status { get; set; }
}

/// <summary>
/// 查询单个任务的请求。
/// Request used to retrieve a single task.
/// </summary>
public class GetTaskByIdRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>任务标识。 / Task identifier.</summary>
    public Guid TaskId { get; set; }
}

/// <summary>
/// 创建任务的请求。
/// Request used to create a task.
/// </summary>
public class CreateTaskRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>任务创建输入。 / Task creation payload.</summary>
    public CreateTaskInput Input { get; set; } = new();
}

/// <summary>
/// 更新任务的请求。
/// Request used to update a task.
/// </summary>
public class UpdateTaskRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>任务标识。 / Task identifier.</summary>
    public Guid TaskId { get; set; }

    /// <summary>任务更新输入。 / Task update payload.</summary>
    public UpdateTaskInput Input { get; set; } = new();
}

/// <summary>
/// 删除任务的请求。
/// Request used to delete a task.
/// </summary>
public class DeleteTaskRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>任务标识。 / Task identifier.</summary>
    public Guid TaskId { get; set; }
}

/// <summary>
/// 恢复阻塞任务的请求。
/// Request used to resume a blocked task.
/// </summary>
public class ResumeBlockedTaskRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>任务标识。 / Task identifier.</summary>
    public Guid TaskId { get; set; }
}

/// <summary>
/// 查询任务时间线的请求。
/// Request used to retrieve a task timeline.
/// </summary>
public class GetTaskTimelineRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>任务标识。 / Task identifier.</summary>
    public Guid TaskId { get; set; }
}

/// <summary>
/// 批量更新任务状态的请求。
/// Request used to update task statuses in batch.
/// </summary>
public class BatchUpdateTaskStatusRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>状态更新项。 / Status update entries.</summary>
    public List<TaskStatusUpdateInput> Updates { get; set; } = [];
}
