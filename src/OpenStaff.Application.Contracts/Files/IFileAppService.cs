using OpenStaff.Application.Contracts.Files.Dtos;

namespace OpenStaff.Application.Contracts.Files;

public interface IFileAppService
{
    Task<List<FileNodeDto>> GetFileTreeAsync(Guid projectId, CancellationToken ct = default);
    Task<string?> GetFileContentAsync(Guid projectId, string path, CancellationToken ct = default);
    Task<string?> GetDiffAsync(Guid projectId, string? commitSha, CancellationToken ct = default);
    Task<List<CheckpointDto>> GetCheckpointsAsync(Guid projectId, CancellationToken ct = default);
}
