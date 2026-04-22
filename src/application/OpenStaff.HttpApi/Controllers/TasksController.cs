
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// 项目任务管理控制器。
/// Controller that exposes project task management endpoints.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
public class TasksController : ControllerBase
{
    private readonly ITaskApiService _taskApiService;

    /// <summary>
    /// 初始化项目任务控制器。
    /// Initializes the project tasks controller.
    /// </summary>
    public TasksController(ITaskApiService taskApiService)
    {
        _taskApiService = taskApiService;
    }

    /// <summary>
    /// 获取项目任务列表。
    /// Gets the task list for a project.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TaskDto>>> GetAll(Guid projectId, [FromQuery] string? status, CancellationToken ct)
        => Ok(await _taskApiService.GetAllAsync(new GetAllTasksRequest { ProjectId = projectId, Status = status }, ct));

    /// <summary>
    /// 获取单个任务详情。
    /// Gets the details of a single task.
    /// </summary>
    [HttpGet("{taskId:guid}")]
    public async Task<ActionResult<TaskDto>> GetById(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var result = await _taskApiService.GetByIdAsync(new GetTaskByIdRequest { ProjectId = projectId, TaskId = taskId }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 创建任务。
    /// Creates a task.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create(Guid projectId, [FromBody] CreateTaskInput input, CancellationToken ct)
        => Ok(await _taskApiService.CreateAsync(new CreateTaskRequest { ProjectId = projectId, Input = input }, ct));

    /// <summary>
    /// 更新任务。
    /// Updates a task.
    /// </summary>
    [HttpPut("{taskId:guid}")]
    public async Task<ActionResult<TaskDto>> Update(Guid projectId, Guid taskId, [FromBody] UpdateTaskInput input, CancellationToken ct)
    {
        var result = await _taskApiService.UpdateAsync(new UpdateTaskRequest { ProjectId = projectId, TaskId = taskId, Input = input }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 删除任务。
    /// Deletes a task.
    /// </summary>
    [HttpDelete("{taskId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid projectId, Guid taskId, CancellationToken ct)
        => await _taskApiService.DeleteAsync(new DeleteTaskRequest { ProjectId = projectId, TaskId = taskId }, ct)
            ? NoContent()
            : NotFound();

    /// <summary>
    /// 恢复被阻塞的任务。
    /// Resumes a blocked task.
    /// </summary>
    [HttpPost("{taskId:guid}/resume")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResumeBlocked(Guid projectId, Guid taskId, CancellationToken ct)
        => await _taskApiService.ResumeBlockedAsync(new ResumeBlockedTaskRequest { ProjectId = projectId, TaskId = taskId }, ct)
            ? Accepted()
            : NotFound();

    /// <summary>
    /// 获取任务时间线。
    /// Gets the runtime timeline of a task.
    /// </summary>
    [HttpGet("{taskId:guid}/timeline")]
    public async Task<ActionResult<List<TaskTimelineDto>>> GetTimeline(Guid projectId, Guid taskId, CancellationToken ct)
        => Ok(await _taskApiService.GetTimelineAsync(new GetTaskTimelineRequest { ProjectId = projectId, TaskId = taskId }, ct));

    /// <summary>
    /// 批量更新任务状态。
    /// Updates task statuses in batch.
    /// </summary>
    [HttpPatch("batch-status")]
    public async Task<ActionResult<UpdatedCountDto>> BatchUpdateStatus(Guid projectId, [FromBody] BatchStatusRequest body, CancellationToken ct)
    {
        var count = await _taskApiService.BatchUpdateStatusAsync(new BatchUpdateTaskStatusRequest { ProjectId = projectId, Updates = body.Tasks }, ct);
        return Ok(new UpdatedCountDto { Updated = count });
    }
}

/// <summary>
/// 批量状态更新请求体。
/// Request body for batch task status updates.
/// </summary>
public class BatchStatusRequest
{
    /// <summary>待更新的任务状态项。 / Task status updates to apply.</summary>
    public List<TaskStatusUpdateInput> Tasks { get; set; } = [];
}

