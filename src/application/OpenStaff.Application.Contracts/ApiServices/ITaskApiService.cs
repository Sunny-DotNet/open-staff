using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// 项目任务应用服务契约。
/// Application service contract for project task management.
/// </summary>
public interface ITaskApiService : IApiServiceBase
{
    /// <summary>
    /// 获取指定项目的任务列表。
    /// Gets the tasks that belong to a project.
    /// </summary>
    Task<List<TaskDto>> GetAllAsync(GetAllTasksRequest request, CancellationToken ct = default);

    /// <summary>
    /// 获取单个任务详情。
    /// Gets the details of a single task.
    /// </summary>
    Task<TaskDto?> GetByIdAsync(GetTaskByIdRequest request, CancellationToken ct = default);

    /// <summary>
    /// 在项目中创建任务。
    /// Creates a task within a project.
    /// </summary>
    Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken ct = default);

    /// <summary>
    /// 更新任务信息。
    /// Updates task metadata and assignment information.
    /// </summary>
    Task<TaskDto?> UpdateAsync(UpdateTaskRequest request, CancellationToken ct = default);

    /// <summary>
    /// 删除任务。
    /// Deletes a task.
    /// </summary>
    Task<bool> DeleteAsync(DeleteTaskRequest request, CancellationToken ct = default);

    /// <summary>
    /// 恢复被阻塞的任务执行。
    /// Resumes execution for a task that is currently blocked.
    /// </summary>
    Task<bool> ResumeBlockedAsync(ResumeBlockedTaskRequest request, CancellationToken ct = default);

    /// <summary>
    /// 获取任务的运行时间线。
    /// Gets the ordered runtime timeline for a task.
    /// </summary>
    Task<List<TaskTimelineDto>> GetTimelineAsync(GetTaskTimelineRequest request, CancellationToken ct = default);

    /// <summary>
    /// 批量更新任务状态并返回受影响条数。
    /// Updates task statuses in batch and returns the affected count.
    /// </summary>
    Task<int> BatchUpdateStatusAsync(BatchUpdateTaskStatusRequest request, CancellationToken ct = default);
}


