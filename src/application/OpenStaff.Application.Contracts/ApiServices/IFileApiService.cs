using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// 项目文件浏览应用服务契约。
/// Application service contract for project file browsing and diffs.
/// </summary>
public interface IFileApiService : IApiServiceBase
{
    /// <summary>
    /// 获取项目文件树。
    /// Gets the project file tree.
    /// </summary>
    Task<List<FileNodeDto>> GetFileTreeAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// 获取文件内容。
    /// Gets the content of a project file.
    /// </summary>
    Task<string?> GetFileContentAsync(GetFileContentRequest request, CancellationToken ct = default);

    /// <summary>
    /// 获取工作区或指定提交的差异。
    /// Gets the working tree diff or the diff for a specific commit.
    /// </summary>
    Task<string?> GetDiffAsync(GetDiffRequest request, CancellationToken ct = default);

    /// <summary>
    /// 获取项目检查点列表。
    /// Gets the checkpoints associated with a project.
    /// </summary>
    Task<List<CheckpointDto>> GetCheckpointsAsync(Guid projectId, CancellationToken ct = default);
}


