using Microsoft.EntityFrameworkCore;
using OpenStaff.Application.Contracts.Tasks;
using OpenStaff.Application.Contracts.Tasks.Dtos;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Application.Tasks;

public class TaskAppService : ITaskAppService
{
    private readonly AppDbContext _db;

    public TaskAppService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<TaskDto>> GetAllAsync(GetAllTasksRequest request, CancellationToken ct)
    {
        var query = _db.Tasks.Where(t => t.ProjectId == request.ProjectId);

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(t => t.Status == request.Status);

        var tasks = await query
            .Include(t => t.AssignedAgent).ThenInclude(a => a!.AgentRole)
            .Include(t => t.SubTasks)
            .OrderBy(t => t.Priority).ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

        return tasks.Select(MapToDto).ToList();
    }

    public async Task<TaskDto?> GetByIdAsync(GetTaskByIdRequest request, CancellationToken ct)
    {
        var task = await _db.Tasks
            .Include(t => t.Dependencies).ThenInclude(d => d.DependsOn)
            .Include(t => t.SubTasks)
            .Include(t => t.AssignedAgent).ThenInclude(a => a!.AgentRole)
            .FirstOrDefaultAsync(t => t.Id == request.TaskId && t.ProjectId == request.ProjectId, ct);

        return task == null ? null : MapToDto(task);
    }

    public async Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { request.ProjectId }, ct);
        if (project == null) throw new KeyNotFoundException("工程不存在 / Project not found");

        var task = new TaskItem
        {
            ProjectId = request.ProjectId,
            Title = request.Input.Title,
            Description = request.Input.Description,
            Priority = request.Input.Priority,
            ParentTaskId = request.Input.ParentTaskId,
            AssignedAgentId = request.Input.AssignedAgentId
        };

        _db.Tasks.Add(task);

        if (request.Input.DependsOn?.Any() == true)
        {
            foreach (var depId in request.Input.DependsOn)
            {
                _db.TaskDependencies.Add(new TaskDependency
                {
                    TaskId = task.Id,
                    DependsOnId = depId
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return MapToDto(task);
    }

    public async Task<TaskDto?> UpdateAsync(UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId && t.ProjectId == request.ProjectId, ct);
        if (task == null) return null;

        if (request.Input.Title != null) task.Title = request.Input.Title;
        if (request.Input.Description != null) task.Description = request.Input.Description;
        if (request.Input.Priority.HasValue) task.Priority = request.Input.Priority.Value;
        if (request.Input.AssignedAgentId.HasValue) task.AssignedAgentId = request.Input.AssignedAgentId.Value;

        if (request.Input.Status != null)
        {
            task.Status = request.Input.Status;
            if (request.Input.Status == "done") task.CompletedAt = DateTime.UtcNow;
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapToDto(task);
    }

    public async Task<bool> DeleteAsync(DeleteTaskRequest request, CancellationToken ct)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId && t.ProjectId == request.ProjectId, ct);
        if (task == null) return false;

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<TaskTimelineDto>> GetTimelineAsync(GetTaskTimelineRequest request, CancellationToken ct)
    {
        var events = await _db.AgentEvents
            .Where(e => e.ProjectId == request.ProjectId && e.Metadata != null && e.Metadata.Contains(request.TaskId.ToString()))
            .OrderBy(e => e.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        return events.Select(e => new TaskTimelineDto
        {
            Id = e.Id,
            EventType = e.EventType,
            Data = e.Content,
            CreatedAt = e.CreatedAt
        }).ToList();
    }

    public async Task<int> BatchUpdateStatusAsync(BatchUpdateTaskStatusRequest request, CancellationToken ct)
    {
        var taskIds = request.Updates.Select(t => t.TaskId).ToList();
        var tasks = await _db.Tasks
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

        await _db.SaveChangesAsync(ct);
        return tasks.Count;
    }

    private static TaskDto MapToDto(TaskItem t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Description = t.Description,
        Status = t.Status,
        Priority = t.Priority,
        AssignedAgentId = t.AssignedAgentId,
        AssignedAgentName = t.AssignedAgent?.AgentRole?.Name,
        AssignedRoleName = t.AssignedAgent?.AgentRole?.RoleType,
        ParentTaskId = t.ParentTaskId,
        SubTasks = t.SubTasks?.Select(MapToDto).ToList(),
        Dependencies = t.Dependencies?.Select(d => d.DependsOnId).ToList(),
        CreatedAt = t.CreatedAt,
        CompletedAt = t.CompletedAt
    };
}
