using Microsoft.EntityFrameworkCore;
using OpenStaff.Application.Contracts.Files;
using OpenStaff.Application.Contracts.Files.Dtos;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Application.Files;

public class FileAppService : IFileAppService
{
    private readonly AppDbContext _db;

    public FileAppService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<FileNodeDto>> GetFileTreeAsync(Guid projectId, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) throw new KeyNotFoundException("Project not found");

        var workspacePath = project.WorkspacePath;
        if (string.IsNullOrEmpty(workspacePath) || !Directory.Exists(workspacePath))
            return [];

        return BuildFileTree(workspacePath, workspacePath, maxDepth: 5);
    }

    public async Task<string?> GetFileContentAsync(GetFileContentRequest request, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { request.ProjectId }, ct);
        if (project == null) throw new KeyNotFoundException("工程不存在 / Project not found");

        var fullPath = Path.GetFullPath(Path.Combine(project.WorkspacePath ?? "", request.Path));
        if (!fullPath.StartsWith(Path.GetFullPath(project.WorkspacePath ?? ""), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("路径越界 / Path traversal detected");

        if (!File.Exists(fullPath))
            return null;

        return await File.ReadAllTextAsync(fullPath, ct);
    }

    public async Task<string?> GetDiffAsync(GetDiffRequest request, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { request.ProjectId }, ct);
        if (project == null) throw new KeyNotFoundException("Project not found");

        var workspacePath = project.WorkspacePath;
        if (string.IsNullOrEmpty(workspacePath)) return "";

        var args = string.IsNullOrEmpty(request.CommitSha) ? "diff" : $"diff {request.CommitSha}^..{request.CommitSha}";
        return RunGit(workspacePath, args);
    }

    public async Task<List<CheckpointDto>> GetCheckpointsAsync(Guid projectId, CancellationToken ct)
    {
        var checkpoints = await _db.Checkpoints
            .Where(c => c.ProjectId == projectId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return checkpoints.Select(c => new CheckpointDto
        {
            Id = c.Id,
            Name = c.Description,
            Description = c.DiffSummary,
            CommitSha = c.GitCommitSha,
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    private static List<FileNodeDto> BuildFileTree(string rootPath, string currentPath, int maxDepth, int depth = 0)
    {
        if (depth >= maxDepth) return [];
        var result = new List<FileNodeDto>();

        try
        {
            foreach (var dir in Directory.GetDirectories(currentPath).OrderBy(d => d))
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith('.') || name == "node_modules" || name == "bin" || name == "obj") continue;

                result.Add(new FileNodeDto
                {
                    Name = name,
                    Path = Path.GetRelativePath(rootPath, dir).Replace('\\', '/'),
                    IsDirectory = true,
                    Children = BuildFileTree(rootPath, dir, maxDepth, depth + 1)
                });
            }

            foreach (var file in Directory.GetFiles(currentPath).OrderBy(f => f))
            {
                result.Add(new FileNodeDto
                {
                    Name = Path.GetFileName(file),
                    Path = Path.GetRelativePath(rootPath, file).Replace('\\', '/'),
                    IsDirectory = false
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
