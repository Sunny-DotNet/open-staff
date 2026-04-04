using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Infrastructure.Export;

/// <summary>
/// 工程导出服务 / Project export service
/// </summary>
public class ProjectExporter
{
    private readonly AppDbContext _db;
    private readonly ILogger<ProjectExporter> _logger;

    public ProjectExporter(AppDbContext db, ILogger<ProjectExporter> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 导出工程为 .openstaff 文件 / Export project as .openstaff file
    /// </summary>
    public async Task<string> ExportAsync(Guid projectId, string outputPath, CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects
            .Include(p => p.Agents).ThenInclude(a => a.AgentRole)
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

        // 写入工程数据
        await WriteJsonEntry(archive, "database/project.json", project);
        await WriteJsonEntry(archive, "database/events.json", project.Events);
        await WriteJsonEntry(archive, "database/tasks.json", project.Tasks);
        await WriteJsonEntry(archive, "database/checkpoints.json", project.Checkpoints);

        // 复制工作空间文件(如果存在)
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

    private static async Task WriteJsonEntry(ZipArchive archive, string entryName, object data)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, data, new JsonSerializerOptions { WriteIndented = true });
    }
}
