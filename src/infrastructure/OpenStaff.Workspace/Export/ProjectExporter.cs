using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Repositories;

namespace OpenStaff.Infrastructure.Export;

/// <summary>
/// 将项目数据库状态与工作区文件打包为可移植归档。
/// Packages project data and workspace files into a portable archive.
/// </summary>
public class ProjectExporter
{
    private readonly IProjectRepository _projects;
    private readonly ILogger<ProjectExporter> _logger;

    /// <summary>
    /// 初始化项目导出服务。
    /// Initializes the project exporter.
    /// </summary>
    public ProjectExporter(IProjectRepository projects, ILogger<ProjectExporter> logger)
    {
        _projects = projects;
        _logger = logger;
    }

    /// <summary>
    /// 将项目导出为 <c>.openstaff</c> 文件。
    /// Exports a project to a <c>.openstaff</c> file.
    /// </summary>
    /// <param name="projectId">要导出的项目标识。/ The project identifier to export.</param>
    /// <param name="outputPath">归档输出目录。/ The archive output directory.</param>
    /// <param name="cancellationToken">异步取消令牌。/ The asynchronous cancellation token.</param>
    /// <returns>生成的归档文件路径。/ The generated archive path.</returns>
    public async Task<string> ExportAsync(Guid projectId, string outputPath, CancellationToken cancellationToken = default)
    {
        // zh-CN: 一次性加载归档所需的核心聚合，避免在导出过程中捕获到不完整的项目快照。
        // en: Load the core aggregate required for archival in one query so the export does not capture a partial project snapshot.
        var project = await _projects
            .Include(p => p.AgentRoles).ThenInclude(a => a.AgentRole)
            .Include(p => p.Tasks)
            .Include(p => p.Events)
            .Include(p => p.Checkpoints)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new InvalidOperationException($"工程不存在: {projectId}");

        var exportFile = Path.Combine(outputPath, $"{project.Name}-{DateTime.UtcNow:yyyyMMddHHmmss}.openstaff");

        using var zipStream = new FileStream(exportFile, FileMode.Create);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        // 写入 manifest
        var manifest = new { Version = "1.0", ProjectName = project.Name, ExportedAt = DateTime.UtcNow };
        await WriteJsonEntry(archive, "manifest.json", manifest);

        // zh-CN: 数据库存档拆分成独立 JSON 条目，便于未来按版本扩展或局部恢复。
        // en: Persist the database snapshot as separate JSON entries so future versions can evolve or restore them independently.
        await WriteJsonEntry(archive, "database/project.json", project);
        await WriteJsonEntry(archive, "database/events.json", project.Events);
        await WriteJsonEntry(archive, "database/tasks.json", project.Tasks);
        await WriteJsonEntry(archive, "database/checkpoints.json", project.Checkpoints);

        // zh-CN: ZIP 中统一使用 workspace/ 前缀，导入时可以重建相对目录结构而不依赖原始绝对路径。
        // en: The ZIP always uses a workspace/ prefix so imports can rebuild relative paths without depending on the original absolute workspace location.
        if (!string.IsNullOrEmpty(project.WorkspacePath) && Directory.Exists(project.WorkspacePath))
        {
            foreach (var file in Directory.GetFiles(project.WorkspacePath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(project.WorkspacePath, file);
                archive.CreateEntryFromFile(file, $"workspace/{relativePath}");
            }
        }

        _logger.LogInformation("工程已导出: {File}", exportFile);
        return exportFile;
    }

    /// <summary>
    /// 将对象写入 ZIP 内的 JSON 文件。
    /// Writes an object into a JSON entry inside the ZIP archive.
    /// </summary>
    private static async Task WriteJsonEntry(ZipArchive archive, string entryName, object data)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, data, new JsonSerializerOptions { WriteIndented = true });
    }
}
