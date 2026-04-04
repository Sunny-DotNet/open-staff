using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// 文件与 Diff 控制器 / File and Diff controller
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/files")]
public class FilesController : ControllerBase
{
    private readonly AppDbContext _db;

    public FilesController(AppDbContext db) => _db = db;

    /// <summary>
    /// 获取项目文件树 / Get project file tree
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFileTree(Guid projectId, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound();

        var workspacePath = project.WorkspacePath;
        if (string.IsNullOrEmpty(workspacePath) || !Directory.Exists(workspacePath))
            return Ok(Array.Empty<object>());

        var tree = BuildFileTree(workspacePath, workspacePath, maxDepth: 5);
        return Ok(tree);
    }

    /// <summary>
    /// 读取文件内容 / Read file content
    /// </summary>
    [HttpGet("content")]
    public async Task<IActionResult> GetFileContent(Guid projectId, [FromQuery] string path, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("工程不存在 / Project not found");

        var fullPath = Path.GetFullPath(Path.Combine(project.WorkspacePath ?? "", path));
        if (!fullPath.StartsWith(Path.GetFullPath(project.WorkspacePath ?? ""), StringComparison.OrdinalIgnoreCase))
            return BadRequest("路径越界 / Path traversal detected");

        if (!System.IO.File.Exists(fullPath))
            return NotFound("文件不存在 / File not found");

        var content = await System.IO.File.ReadAllTextAsync(fullPath, ct);
        return Ok(new { path, content });
    }

    /// <summary>
    /// 获取 Git Diff / Get Git diff
    /// </summary>
    [HttpGet("diff")]
    public async Task<IActionResult> GetDiff(Guid projectId, [FromQuery] string? commitSha, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound();

        var workspacePath = project.WorkspacePath;
        if (string.IsNullOrEmpty(workspacePath)) return Ok(new { diff = "" });

        var args = string.IsNullOrEmpty(commitSha) ? "diff" : $"diff {commitSha}^..{commitSha}";
        var result = RunGit(workspacePath, args);

        return Ok(new { diff = result });
    }

    /// <summary>
    /// 获取检查点列表 / Get checkpoints list
    /// </summary>
    [HttpGet("~/api/projects/{projectId:guid}/checkpoints")]
    public async Task<IActionResult> GetCheckpoints(Guid projectId, CancellationToken ct)
    {
        var checkpoints = await _db.Checkpoints
            .Where(c => c.ProjectId == projectId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
        return Ok(checkpoints);
    }

    private static List<object> BuildFileTree(string rootPath, string currentPath, int maxDepth, int depth = 0)
    {
        if (depth >= maxDepth) return new();
        var result = new List<object>();

        try
        {
            foreach (var dir in Directory.GetDirectories(currentPath).OrderBy(d => d))
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith('.') || name == "node_modules" || name == "bin" || name == "obj") continue;

                result.Add(new
                {
                    name,
                    path = Path.GetRelativePath(rootPath, dir).Replace('\\', '/'),
                    isDirectory = true,
                    children = BuildFileTree(rootPath, dir, maxDepth, depth + 1)
                });
            }

            foreach (var file in Directory.GetFiles(currentPath).OrderBy(f => f))
            {
                result.Add(new
                {
                    name = Path.GetFileName(file),
                    path = Path.GetRelativePath(rootPath, file).Replace('\\', '/'),
                    isDirectory = false
                });
            }
        }
        catch { /* Permission errors */ }

        return result;
    }

    private static string RunGit(string workDir, string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", args)
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return "";
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10_000);
            return output;
        }
        catch { return ""; }
    }
}
