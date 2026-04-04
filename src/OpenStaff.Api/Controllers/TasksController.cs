using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// 任务管理控制器 / Tasks controller
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;

    public TasksController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid projectId, [FromQuery] string? status, CancellationToken ct)
    {
        var query = _db.Tasks.Where(t => t.ProjectId == projectId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        var tasks = await query
            .Include(t => t.AssignedAgent).ThenInclude(a => a!.AgentRole)
            .Include(t => t.SubTasks)
            .OrderBy(t => t.Priority).ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);
        return Ok(tasks);
    }

    [HttpGet("{taskId:guid}")]
    public async Task<IActionResult> GetById(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var task = await _db.Tasks
            .Include(t => t.Dependencies).ThenInclude(d => d.DependsOn)
            .Include(t => t.SubTasks)
            .Include(t => t.AssignedAgent).ThenInclude(a => a!.AgentRole)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, ct);
        if (task == null) return NotFound();
        return Ok(task);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateTaskRequest request, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("工程不存在 / Project not found");

        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            ParentTaskId = request.ParentTaskId,
            AssignedAgentId = request.AssignedAgentId
        };

        _db.Tasks.Add(task);

        // 添加依赖关系 / Add dependencies
        if (request.DependsOn?.Any() == true)
        {
            foreach (var depId in request.DependsOn)
            {
                _db.TaskDependencies.Add(new TaskDependency
                {
                    TaskId = task.Id,
                    DependsOnId = depId
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { projectId, taskId = task.Id }, task);
    }

    [HttpPut("{taskId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid taskId, [FromBody] UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, ct);
        if (task == null) return NotFound();

        if (request.Title != null) task.Title = request.Title;
        if (request.Description != null) task.Description = request.Description;
        if (request.Priority.HasValue) task.Priority = request.Priority.Value;
        if (request.AssignedAgentId.HasValue) task.AssignedAgentId = request.AssignedAgentId.Value;

        if (request.Status != null)
        {
            task.Status = request.Status;
            if (request.Status == "done") task.CompletedAt = DateTime.UtcNow;
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(task);
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, ct);
        if (task == null) return NotFound();

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// 获取任务时间线 / Get task timeline (events related to this task)
    /// </summary>
    [HttpGet("{taskId:guid}/timeline")]
    public async Task<IActionResult> GetTimeline(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var events = await _db.AgentEvents
            .Where(e => e.ProjectId == projectId && e.Metadata != null && e.Metadata.Contains(taskId.ToString()))
            .OrderBy(e => e.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return Ok(events);
    }

    /// <summary>批量更新任务状态（看板拖拽）</summary>
    [HttpPatch("batch-status")]
    public async Task<IActionResult> BatchUpdateStatus(Guid projectId, [FromBody] BatchStatusRequest request, CancellationToken ct)
    {
        var taskIds = request.Tasks.Select(t => t.TaskId).ToList();
        var tasks = await _db.Tasks
            .Where(t => t.ProjectId == projectId && taskIds.Contains(t.Id))
            .ToListAsync(ct);

        foreach (var update in request.Tasks)
        {
            var task = tasks.FirstOrDefault(t => t.Id == update.TaskId);
            if (task != null)
            {
                task.Status = update.Status;
                task.UpdatedAt = DateTime.UtcNow;
                if (update.Status == TaskItemStatus.Done) task.CompletedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { updated = tasks.Count });
    }
}

public class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Priority { get; set; }
    public Guid? ParentTaskId { get; set; }
    public Guid? AssignedAgentId { get; set; }
    public List<Guid>? DependsOn { get; set; }
}

public class UpdateTaskRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public int? Priority { get; set; }
    public Guid? AssignedAgentId { get; set; }
}

public class BatchStatusRequest
{
    public List<TaskStatusUpdate> Tasks { get; set; } = new();
}

public class TaskStatusUpdate
{
    public Guid TaskId { get; set; }
    public string Status { get; set; } = string.Empty;
}
