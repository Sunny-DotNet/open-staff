using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Repositories;

namespace OpenStaff.Infrastructure.Export;

/// <summary>
/// 从归档恢复项目元数据与工作区文件。
/// Restores project metadata and workspace files from an archive.
/// </summary>
public class ProjectImporter
{
    private readonly IProjectRepository _projects;
    private readonly IRepositoryContext _repositoryContext;
    private readonly ILogger<ProjectImporter> _logger;

    /// <summary>
    /// 初始化项目导入服务。
    /// Initializes the project importer.
    /// </summary>
    public ProjectImporter(
        IProjectRepository projects,
        IRepositoryContext repositoryContext,
        ILogger<ProjectImporter> logger)
    {
        _projects = projects;
        _repositoryContext = repositoryContext;
        _logger = logger;
    }

    /// <summary>
    /// 从 <c>.openstaff</c> 文件导入项目。
    /// Imports a project from a <c>.openstaff</c> file.
    /// </summary>
    /// <param name="filePath">归档文件路径。/ The archive file path.</param>
    /// <param name="workspacesRoot">新工作区根目录。/ The root directory where the new workspace should be created.</param>
    /// <param name="cancellationToken">异步取消令牌。/ The asynchronous cancellation token.</param>
    /// <returns>新建项目的标识。/ The identifier of the imported project.</returns>
    public async Task<Guid> ImportAsync(string filePath, string workspacesRoot, CancellationToken cancellationToken = default)
    {
        using var zipStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        // zh-CN: 导入包至少需要项目元数据；其余数据集可以在后续版本逐步扩展。
        // en: The import package must contain project metadata; additional datasets can be expanded in later versions.
        var projectEntry = archive.GetEntry("database/project.json")
            ?? throw new InvalidOperationException("导入文件格式无效: 缺少 project.json");

        using var projectStream = projectEntry.Open();
        var project = await JsonSerializer.DeserializeAsync<Project>(projectStream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("无法解析工程数据");

        // zh-CN: 导入始终分配新的主键与时间戳，避免覆盖现有记录或复用历史身份。
        // en: Imports always receive new keys and timestamps so they never overwrite existing records or reuse historical identities.
        var newProjectId = Guid.NewGuid();
        project.Id = newProjectId;
        project.CreatedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;

        // zh-CN: 工作区总是指向新的根目录，确保导入结果与导出机器上的绝对路径解耦。
        // en: The workspace always points to a fresh root so the imported project is decoupled from absolute paths on the exporting machine.
        var newWorkspacePath = Path.Combine(workspacesRoot, newProjectId.ToString());
        project.WorkspacePath = newWorkspacePath;

        // zh-CN: 仅还原 workspace/ 下的内容，并验证规范化路径仍位于目标工作区内，避免 ZIP Slip 覆盖任意文件。
        // en: Restore only workspace/ entries and verify the normalized path stays inside the target workspace to prevent ZIP Slip overwrites.
        var normalizedWorkspaceRoot = Path.GetFullPath(newWorkspacePath);
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("workspace/")))
        {
            var relativePath = entry.FullName["workspace/".Length..];
            if (string.IsNullOrEmpty(relativePath)) continue;

            var targetPath = Path.Combine(newWorkspacePath, relativePath);
            var normalizedTargetPath = Path.GetFullPath(targetPath);
            if (!normalizedTargetPath.StartsWith(normalizedWorkspaceRoot + Path.DirectorySeparatorChar, pathComparison)
                && !string.Equals(normalizedTargetPath, normalizedWorkspaceRoot, pathComparison))
            {
                throw new InvalidOperationException($"导入文件格式无效: 非法工作区路径 {relativePath}");
            }

            var targetDir = Path.GetDirectoryName(normalizedTargetPath);
            if (targetDir != null) Directory.CreateDirectory(targetDir);

            if (!entry.FullName.EndsWith('/'))
            {
                entry.ExtractToFile(normalizedTargetPath, overwrite: true);
            }
        }

        _projects.Add(project);
        await _repositoryContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("工程已导入: {ProjectId} ({Name})", newProjectId, project.Name);
        return newProjectId;
    }
}
