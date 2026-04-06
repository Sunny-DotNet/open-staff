using OpenStaff.Application.Contracts.Tasks.Dtos;

namespace OpenStaff.Application.Contracts.Tasks;

public interface ITaskAppService
{
    Task<List<TaskDto>> GetAllAsync(GetAllTasksRequest request, CancellationToken ct = default);
    Task<TaskDto?> GetByIdAsync(GetTaskByIdRequest request, CancellationToken ct = default);
    Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken ct = default);
    Task<TaskDto?> UpdateAsync(UpdateTaskRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(DeleteTaskRequest request, CancellationToken ct = default);
    Task<List<TaskTimelineDto>> GetTimelineAsync(GetTaskTimelineRequest request, CancellationToken ct = default);
    Task<int> BatchUpdateStatusAsync(BatchUpdateTaskStatusRequest request, CancellationToken ct = default);
}
