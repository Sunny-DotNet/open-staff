using OpenStaff.Application.Contracts.Projects.Dtos;

namespace OpenStaff.Application.Contracts.Projects;

public interface IProjectAppService
{
    Task<List<ProjectDto>> GetAllAsync(CancellationToken ct = default);
    Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto> CreateAsync(CreateProjectInput input, CancellationToken ct = default);
    Task<ProjectDto?> UpdateAsync(Guid id, UpdateProjectInput input, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto?> InitializeAsync(Guid id, CancellationToken ct = default);
    Task<string?> ExportAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto?> ImportAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}
