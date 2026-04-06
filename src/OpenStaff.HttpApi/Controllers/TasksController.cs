using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Contracts.Tasks;
using OpenStaff.Application.Contracts.Tasks.Dtos;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
public class TasksController : ControllerBase
{
    private readonly ITaskAppService _taskAppService;

    public TasksController(ITaskAppService taskAppService)
    {
        _taskAppService = taskAppService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid projectId, [FromQuery] string? status, CancellationToken ct)
        => Ok(await _taskAppService.GetAllAsync(new GetAllTasksRequest { ProjectId = projectId, Status = status }, ct));

    [HttpGet("{taskId:guid}")]
    public async Task<IActionResult> GetById(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var result = await _taskAppService.GetByIdAsync(new GetTaskByIdRequest { ProjectId = projectId, TaskId = taskId }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateTaskInput input, CancellationToken ct)
        => Ok(await _taskAppService.CreateAsync(new CreateTaskRequest { ProjectId = projectId, Input = input }, ct));

    [HttpPut("{taskId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid taskId, [FromBody] UpdateTaskInput input, CancellationToken ct)
    {
        var result = await _taskAppService.UpdateAsync(new UpdateTaskRequest { ProjectId = projectId, TaskId = taskId, Input = input }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid taskId, CancellationToken ct)
        => await _taskAppService.DeleteAsync(new DeleteTaskRequest { ProjectId = projectId, TaskId = taskId }, ct) ? NoContent() : NotFound();

    [HttpGet("{taskId:guid}/timeline")]
    public async Task<IActionResult> GetTimeline(Guid projectId, Guid taskId, CancellationToken ct)
        => Ok(await _taskAppService.GetTimelineAsync(new GetTaskTimelineRequest { ProjectId = projectId, TaskId = taskId }, ct));

    [HttpPatch("batch-status")]
    public async Task<IActionResult> BatchUpdateStatus(Guid projectId, [FromBody] BatchStatusRequest body, CancellationToken ct)
    {
        var count = await _taskAppService.BatchUpdateStatusAsync(new BatchUpdateTaskStatusRequest { ProjectId = projectId, Updates = body.Tasks }, ct);
        return Ok(new { updated = count });
    }
}

public class BatchStatusRequest
{
    public List<TaskStatusUpdateInput> Tasks { get; set; } = [];
}
