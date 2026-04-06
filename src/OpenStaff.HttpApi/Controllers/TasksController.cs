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
        => Ok(await _taskAppService.GetAllAsync(projectId, status, ct));

    [HttpGet("{taskId:guid}")]
    public async Task<IActionResult> GetById(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var result = await _taskAppService.GetByIdAsync(projectId, taskId, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateTaskInput input, CancellationToken ct)
        => Ok(await _taskAppService.CreateAsync(projectId, input, ct));

    [HttpPut("{taskId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid taskId, [FromBody] UpdateTaskInput input, CancellationToken ct)
    {
        var result = await _taskAppService.UpdateAsync(projectId, taskId, input, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid taskId, CancellationToken ct)
        => await _taskAppService.DeleteAsync(projectId, taskId, ct) ? NoContent() : NotFound();

    [HttpGet("{taskId:guid}/timeline")]
    public async Task<IActionResult> GetTimeline(Guid projectId, Guid taskId, CancellationToken ct)
        => Ok(await _taskAppService.GetTimelineAsync(projectId, taskId, ct));

    [HttpPatch("batch-status")]
    public async Task<IActionResult> BatchUpdateStatus(Guid projectId, [FromBody] BatchStatusRequest request, CancellationToken ct)
    {
        var count = await _taskAppService.BatchUpdateStatusAsync(projectId, request.Tasks, ct);
        return Ok(new { updated = count });
    }
}

public class BatchStatusRequest
{
    public List<TaskStatusUpdateInput> Tasks { get; set; } = [];
}
