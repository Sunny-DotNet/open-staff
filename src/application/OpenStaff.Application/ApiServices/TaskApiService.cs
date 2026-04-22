namespace OpenStaff.ApiServices;
/// <summary>
/// 任务应用服务实现。
/// Application service implementation for project task management.
/// </summary>
public class TaskApiService : ApiServiceBase, ITaskApiService
{
    private readonly ITaskItemRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly ITaskDependencyRepository _taskDependencies;
    private readonly IAgentEventRepository _agentEvents;
    private readonly IRepositoryContext _repositoryContext;
    private readonly SessionRunner _sessionRunner;

    /// <summary>
    /// 初始化任务应用服务。
    /// Initializes the task application service.
    /// </summary>
    public TaskApiService(
        ITaskItemRepository tasks,
        IProjectRepository projects,
        ITaskDependencyRepository taskDependencies,
        IAgentEventRepository agentEvents,
        IRepositoryContext repositoryContext,
        SessionRunner sessionRunner,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _tasks = tasks;
        _projects = projects;
        _taskDependencies = taskDependencies;
        _agentEvents = agentEvents;
        _repositoryContext = repositoryContext;
        _sessionRunner = sessionRunner;
    }

    /// <inheritdoc />
    public async Task<List<TaskDto>> GetAllAsync(GetAllTasksRequest request, CancellationToken ct)
    {
        var query = _tasks.Where(t => t.ProjectId == request.ProjectId);

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(t => t.Status == request.Status);

        var tasks = await query
            .Include(t => t.AssignedProjectAgentRole).ThenInclude(a => a!.AgentRole)
            .Include(t => t.SubTasks)
            .OrderBy(t => t.Priority).ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

        return tasks.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<TaskDto?> GetByIdAsync(GetTaskByIdRequest request, CancellationToken ct)
    {
        var task = await _tasks
            .Include(t => t.Dependencies).ThenInclude(d => d.DependsOn)
            .Include(t => t.SubTasks)
            .Include(t => t.AssignedProjectAgentRole).ThenInclude(a => a!.AgentRole)
            .FirstOrDefaultAsync(t => t.Id == request.TaskId && t.ProjectId == request.ProjectId, ct);

        return task == null ? null : MapToDto(task);
    }

    /// <inheritdoc />
    public async Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken ct)
    {
        var project = await _projects.FindAsync(request.ProjectId, ct);
        if (project == null) throw new KeyNotFoundException("工程不存在 / Project not found");

        var task = new TaskItem
        {
            ProjectId = request.ProjectId,
            Title = request.Input.Title,
            Description = request.Input.Description,
            Priority = request.Input.Priority,
            ParentTaskId = request.Input.ParentTaskId,
            AssignedProjectAgentRoleId = request.Input.AssignedProjectAgentRoleId
        };

        _tasks.Add(task);

        if (request.Input.DependsOn?.Any() == true)
        {
            foreach (var depId in request.Input.DependsOn)
            {
                _taskDependencies.Add(new TaskDependency
                {
                    TaskId = task.Id,
                    DependsOnId = depId
                });
            }
        }

        await _repositoryContext.SaveChangesAsync(ct);
        return MapToDto(task);
    }

    /// <inheritdoc />
    public async Task<TaskDto?> UpdateAsync(UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await _tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId && t.ProjectId == request.ProjectId, ct);
        if (task == null) return null;

        if (request.Input.Title != null) task.Title = request.Input.Title;
        if (request.Input.Description != null) task.Description = request.Input.Description;
        if (request.Input.Priority.HasValue) task.Priority = request.Input.Priority.Value;
        if (request.Input.AssignedProjectAgentRoleId.HasValue) task.AssignedProjectAgentRoleId = request.Input.AssignedProjectAgentRoleId.Value;

        if (request.Input.Status != null)
        {
            task.Status = request.Input.Status;
            if (request.Input.Status == "done") task.CompletedAt = DateTime.UtcNow;
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _repositoryContext.SaveChangesAsync(ct);
        return MapToDto(task);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(DeleteTaskRequest request, CancellationToken ct)
    {
        var task = await _tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId && t.ProjectId == request.ProjectId, ct);
        if (task == null) return false;

        _tasks.Remove(task);
        await _repositoryContext.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ResumeBlockedAsync(ResumeBlockedTaskRequest request, CancellationToken ct)
    {
        var task = await _tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == request.TaskId
                    && item.ProjectId == request.ProjectId
                    && item.Status == TaskItemStatus.Blocked,
                ct);
        if (task == null)
            return false;

        return await _sessionRunner.ResumeBlockedProjectGroupTaskAsync(request.ProjectId, request.TaskId, ct);
    }

    /// <inheritdoc />
    public async Task<List<TaskTimelineDto>> GetTimelineAsync(GetTaskTimelineRequest request, CancellationToken ct)
    {
        var events = await _agentEvents
            .Where(e => e.ProjectId == request.ProjectId && e.Metadata != null && e.Metadata.Contains(request.TaskId.ToString()))
            .OrderBy(e => e.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        return events.Select(MapTimelineEvent).ToList();
    }

    /// <inheritdoc />
    public async Task<int> BatchUpdateStatusAsync(BatchUpdateTaskStatusRequest request, CancellationToken ct)
    {
        var taskIds = request.Updates.Select(t => t.TaskId).ToList();
        var tasks = await _tasks
            .Where(t => t.ProjectId == request.ProjectId && taskIds.Contains(t.Id))
            .ToListAsync(ct);

        foreach (var update in request.Updates)
        {
            var task = tasks.FirstOrDefault(t => t.Id == update.TaskId);
            if (task != null)
            {
                task.Status = update.Status;
                task.UpdatedAt = DateTime.UtcNow;
                if (update.Status == TaskItemStatus.Done) task.CompletedAt = DateTime.UtcNow;
            }
        }

        await _repositoryContext.SaveChangesAsync(ct);
        return tasks.Count;
    }

    /// <summary>
    /// Maps a task entity and its parsed runtime metadata to the API DTO, recursively projecting subtasks and dependency identifiers for UI consumption.
    /// 将任务实体及其解析后的运行时元数据映射为 API DTO，并递归投影子任务与依赖标识供界面使用。
    /// </summary>
    /// <param name="t">Task entity to project. / 要投影的任务实体。</param>
    /// <returns>Task DTO enriched with parsed runtime metadata. / 携带解析后运行时元数据的任务 DTO。</returns>
    private static TaskDto MapToDto(TaskItem t)
    {
        var metadata = RuntimeProjectionMetadataMapper.ParseTaskMetadata(t.Metadata);
        return new TaskDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Status = t.Status,
            Priority = t.Priority,
            AssignedProjectAgentRoleId = t.AssignedProjectAgentRoleId,
            AssignedAgentName = t.AssignedProjectAgentRole?.AgentRole?.Name,
            AssignedRoleName = t.AssignedProjectAgentRole?.AgentRole?.Name,
            ParentTaskId = t.ParentTaskId,
            SubTasks = t.SubTasks?.Select(MapToDto).ToList(),
            Dependencies = t.Dependencies?.Select(d => d.DependsOnId).ToList(),
            SessionId = metadata?.SessionId,
            ExecutionPackageId = metadata?.ExecutionPackageId,
            FrameId = metadata?.FrameId,
            MessageId = metadata?.MessageId,
            Scene = RuntimeProjectionMetadataMapper.NormalizeScene(metadata?.Scene),
            EntryKind = metadata?.EntryKind,
            MentionedAgentRoleId = metadata?.MentionedAgentRoleId,
            MentionedProjectAgentRoleId = metadata?.MentionedProjectAgentRoleId,
            DispatchSource = metadata?.Source,
            TargetAgentRoleId = metadata?.TargetAgentRoleId,
            TargetProjectAgentRoleId = metadata?.TargetProjectAgentRoleId,
            LastStatus = metadata?.LastStatus,
            SourceFrameId = metadata?.SourceFrameId,
            SourceEffectIndex = metadata?.SourceEffectIndex,
            LastResult = metadata?.LastResult,
            LastError = metadata?.LastError,
            Model = metadata?.Model,
            AttemptCount = metadata?.AttemptCount ?? 0,
            TotalTokens = metadata?.TotalTokens,
            DurationMs = metadata?.DurationMs,
            FirstTokenMs = metadata?.FirstTokenMs,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            CompletedAt = t.CompletedAt
        };
    }

    /// <summary>
    /// Maps an agent event into a task timeline DTO by reading runtime metadata fields such as scene, role, tokens, and latency.
    /// 通过读取场景、角色、令牌和时延等运行时元数据字段，将代理事件映射为任务时间线 DTO。
    /// </summary>
    /// <param name="agentEvent">Agent event captured for the task timeline. / 为任务时间线捕获的代理事件。</param>
    /// <returns>Timeline DTO projected from the event and its metadata. / 由事件及其元数据投影得到的时间线 DTO。</returns>
    private static TaskTimelineDto MapTimelineEvent(AgentEvent agentEvent)
    {
        var metadata = RuntimeProjectionMetadataMapper.ParseAgentEventMetadata(agentEvent.Metadata);
        return new TaskTimelineDto
        {
            Id = agentEvent.Id,
            EventType = agentEvent.EventType,
            Data = agentEvent.Content,
            Content = agentEvent.Content,
            Metadata = agentEvent.Metadata,
            TaskId = metadata?.TaskId,
            SessionId = metadata?.SessionId,
            ExecutionPackageId = metadata?.ExecutionPackageId,
            FrameId = metadata?.FrameId,
            MessageId = metadata?.MessageId,
            Scene = RuntimeProjectionMetadataMapper.NormalizeScene(metadata?.Scene),
            EntryKind = metadata?.EntryKind,
            AgentRoleId = metadata?.AgentRoleId,
            ProjectAgentRoleId = metadata?.ProjectAgentRoleId,
            TargetAgentRoleId = metadata?.TargetAgentRoleId,
            TargetProjectAgentRoleId = metadata?.TargetProjectAgentRoleId,
            Model = metadata?.Model,
            ToolName = metadata?.ToolName,
            ToolCallId = metadata?.ToolCallId,
            Status = metadata?.Status,
            SourceFrameId = metadata?.SourceFrameId,
            SourceEffectIndex = metadata?.SourceEffectIndex,
            Detail = metadata?.Detail,
            Attempt = metadata?.Attempt,
            MaxAttempts = metadata?.MaxAttempts,
            TotalTokens = metadata?.TotalTokens,
            DurationMs = metadata?.DurationMs,
            FirstTokenMs = metadata?.FirstTokenMs,
            CreatedAt = agentEvent.CreatedAt
        };
    }
}





