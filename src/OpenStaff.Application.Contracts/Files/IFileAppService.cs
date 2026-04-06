using OpenStaff.Application.Contracts.Files.Dtos;

namespace OpenStaff.Application.Contracts.Files;

public interface IFileAppService
{
    Task<List<FileNodeDto>> GetFileTreeAsync(Guid projectId, CancellationToken ct = default);
    Task<string?> GetFileContentAsync(GetFileContentRequest request, CancellationToken ct = default);
    Task<string?> GetDiffAsync(GetDiffRequest request, CancellationToken ct = default);
    Task<List<CheckpointDto>> GetCheckpointsAsync(Guid projectId, CancellationToken ct = default);
}
