using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Infrastructure.Export;

/// <summary>
/// 工程导入服务 / Project import service
/// </summary>
public class ProjectImporter
{
    private readonly AppDbContext _db;
    private readonly ILogger<ProjectImporter> _logger;

    public ProjectImporter(AppDbContext db, ILogger<ProjectImporter> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 从 .openstaff 文件导入工程 / Import project from .openstaff file
    /// </summary>
    public async Task<Guid> ImportAsync(string filePath, string workspacesRoot, CancellationToken cancellationToken = default)
    {
        using var zipStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        // 读取工程数据
        var projectEntry = archive.GetEntry("database/project.json")
            ?? throw new InvalidOperationException("导入文件格式无效: 缺少 project.json");

        using var projectStream = projectEntry.Open();
        var project = await JsonSerializer.DeserializeAsync<Project>(projectStream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("无法解析工程数据");

        // 生成新的 ID 避免冲突
        var newProjectId = Guid.NewGuid();
        project.Id = newProjectId;
        project.CreatedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;

        // 设置新的工作空间路径
        var newWorkspacePath = Path.Combine(workspacesRoot, newProjectId.ToString());
        project.WorkspacePath = newWorkspacePath;

        // 解压工作空间文件
        foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("workspace/")))
        {
            var relativePath = entry.FullName["workspace/".Length..];
            if (string.IsNullOrEmpty(relativePath)) continue;

            var targetPath = Path.Combine(newWorkspacePath, relativePath);
            var targetDir = Path.GetDirectoryName(targetPath);
            if (targetDir != null) Directory.CreateDirectory(targetDir);

            if (!entry.FullName.EndsWith('/'))
            {
                entry.ExtractToFile(targetPath, overwrite: true);
            }
        }

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("工程已导入: {ProjectId} ({Name})", newProjectId, project.Name);
        return newProjectId;
    }
}
