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

    public async Task<List<TaskDto>> GetAllAsync(Guid projectId, string? status, CancellationToken ct)
    {
        var query = _db.Tasks.Where(t => t.ProjectId == projectId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        var tasks = await query
            .Include(t => t.AssignedAgent).ThenInclude(a => a!.AgentRole)
            .Include(t => t.SubTasks)
            .OrderBy(t => t.Priority).ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

        return tasks.Select(MapToDto).ToList();
    }

    public async Task<TaskDto?> GetByIdAsync(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var task = await _db.Tasks
            .Include(t => t.Dependencies).ThenInclude(d => d.DependsOn)
            .Include(t => t.SubTasks)
            .Include(t => t.AssignedAgent).ThenInclude(a => a!.AgentRole)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, ct);

        return task == null ? null : MapToDto(task);
    }

    public async Task<TaskDto> CreateAsync(Guid projectId, CreateTaskInput input, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) throw new KeyNotFoundException("工程不存在 / Project not found");

        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = input.Title,
            Description = input.Description,
            Priority = input.Priority,
            ParentTaskId = input.ParentTaskId,
            AssignedAgentId = input.AssignedAgentId
        };

        _db.Tasks.Add(task);

        if (input.DependsOn?.Any() == true)
        {
            foreach (var depId in input.DependsOn)
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

    public async Task<TaskDto?> UpdateAsync(Guid projectId, Guid taskId, UpdateTaskInput input, CancellationToken ct)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, ct);
        if (task == null) return null;

        if (input.Title != null) task.Title = input.Title;
        if (input.Description != null) task.Description = input.Description;
        if (input.Priority.HasValue) task.Priority = input.Priority.Value;
        if (input.AssignedAgentId.HasValue) task.AssignedAgentId = input.AssignedAgentId.Value;

        if (input.Status != null)
        {
            task.Status = input.Status;
            if (input.Status == "done") task.CompletedAt = DateTime.UtcNow;
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapToDto(task);
    }

    public async Task<bool> DeleteAsync(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, ct);
        if (task == null) return false;

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<TaskTimelineDto>> GetTimelineAsync(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var events = await _db.AgentEvents
            .Where(e => e.ProjectId == projectId && e.Metadata != null && e.Metadata.Contains(taskId.ToString()))
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

    public async Task<int> BatchUpdateStatusAsync(Guid projectId, List<TaskStatusUpdateInput> updates, CancellationToken ct)
    {
        var taskIds = updates.Select(t => t.TaskId).ToList();
        var tasks = await _db.Tasks
            .Where(t => t.ProjectId == projectId && taskIds.Contains(t.Id))
            .ToListAsync(ct);

        foreach (var update in updates)
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
