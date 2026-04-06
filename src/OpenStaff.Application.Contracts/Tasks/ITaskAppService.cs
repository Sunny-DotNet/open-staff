using OpenStaff.Application.Contracts.Tasks.Dtos;

namespace OpenStaff.Application.Contracts.Tasks;

public interface ITaskAppService
{
    Task<List<TaskDto>> GetAllAsync(Guid projectId, string? status = null, CancellationToken ct = default);
    Task<TaskDto?> GetByIdAsync(Guid projectId, Guid taskId, CancellationToken ct = default);
    Task<TaskDto> CreateAsync(Guid projectId, CreateTaskInput input, CancellationToken ct = default);
    Task<TaskDto?> UpdateAsync(Guid projectId, Guid taskId, UpdateTaskInput input, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid projectId, Guid taskId, CancellationToken ct = default);
    Task<List<TaskTimelineDto>> GetTimelineAsync(Guid projectId, Guid taskId, CancellationToken ct = default);
    Task<int> BatchUpdateStatusAsync(Guid projectId, List<TaskStatusUpdateInput> updates, CancellationToken ct = default);
}
