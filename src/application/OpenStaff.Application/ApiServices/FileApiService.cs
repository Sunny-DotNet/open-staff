namespace OpenStaff.ApiServices;
/// <summary>
/// 项目文件应用服务实现。
/// Application service implementation for project file browsing.
/// </summary>
public class FileApiService : ApiServiceBase, IFileApiService
{
    private readonly IProjectRepository _projects;
    private readonly ICheckpointRepository _checkpoints;

    /// <summary>
    /// 初始化项目文件应用服务。
    /// Initializes the project file application service.
    /// </summary>
    public FileApiService(
        IProjectRepository projects,
        ICheckpointRepository checkpoints,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _projects = projects;
        _checkpoints = checkpoints;
    }

    /// <inheritdoc />
    public async Task<List<FileNodeDto>> GetFileTreeAsync(Guid projectId, CancellationToken ct)
    {
        var project = await _projects.FindAsync(projectId, ct);
        if (project == null) throw new KeyNotFoundException("Project not found");

        var workspacePath = project.WorkspacePath;
        if (string.IsNullOrEmpty(workspacePath) || !Directory.Exists(workspacePath))
            return [];

        return BuildFileTree(workspacePath, workspacePath, maxDepth: 5);
    }

    /// <inheritdoc />
    public async Task<string?> GetFileContentAsync(GetFileContentRequest request, CancellationToken ct)
    {
        var project = await _projects.FindAsync(request.ProjectId, ct);
        if (project == null) throw new KeyNotFoundException("工程不存在 / Project not found");

        var fullPath = Path.GetFullPath(Path.Combine(project.WorkspacePath ?? "", request.Path));
        // zh-CN: 读取文件前必须把解析后的路径限制在工作区根目录内，避免路径穿越访问任意文件。
        // en: Constrain the resolved path to the workspace root before reading so path traversal cannot escape into arbitrary files.
        if (!fullPath.StartsWith(Path.GetFullPath(project.WorkspacePath ?? ""), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("路径越界 / Path traversal detected");

        if (!File.Exists(fullPath))
            return null;

        return await File.ReadAllTextAsync(fullPath, ct);
    }

    /// <inheritdoc />
    public async Task<string?> GetDiffAsync(GetDiffRequest request, CancellationToken ct)
    {
        var project = await _projects.FindAsync(request.ProjectId, ct);
        if (project == null) throw new KeyNotFoundException("Project not found");

        var workspacePath = project.WorkspacePath;
        if (string.IsNullOrEmpty(workspacePath)) return "";

        var args = string.IsNullOrEmpty(request.CommitSha) ? "diff" : $"diff {request.CommitSha}^..{request.CommitSha}";
        return RunGit(workspacePath, args);
    }

    /// <inheritdoc />
    public async Task<List<CheckpointDto>> GetCheckpointsAsync(Guid projectId, CancellationToken ct)
    {
        var checkpoints = await _checkpoints
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

    /// <summary>
    /// 递归构建工作区文件树。
    /// Recursively builds the workspace file tree.
    /// </summary>
    /// <param name="rootPath">工作区根目录。/ Workspace root directory.</param>
    /// <param name="currentPath">当前枚举的目录。/ Directory currently being enumerated.</param>
    /// <param name="maxDepth">允许遍历的最大深度。/ Maximum traversal depth.</param>
    /// <param name="depth">当前递归深度。/ Current recursion depth.</param>
    /// <returns>当前层级下的文件树节点集合。/ The file-tree nodes discovered at the current level.</returns>
    /// <remarks>
    /// zh-CN: 该方法只执行文件系统读取，按名称排序并跳过隐藏目录及常见构建目录；遇到权限异常时会吞掉异常以保持浏览体验。
    /// en: This method performs filesystem reads only, orders entries by name, skips hidden/common build directories, and suppresses permission errors to keep browsing resilient.
    /// </remarks>
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

    /// <summary>
    /// 在指定工作区中执行 Git 命令并返回标准输出。
    /// Executes a Git command inside the specified workspace and returns standard output.
    /// </summary>
    /// <param name="workDir">Git 命令的工作目录。/ Working directory for the Git process.</param>
    /// <param name="args">传递给 Git 的参数。/ Arguments passed to Git.</param>
    /// <returns>捕获到的标准输出；若启动或执行失败则返回空字符串。/ Captured standard output, or an empty string when startup or execution fails.</returns>
    /// <remarks>
    /// zh-CN: 该方法会启动一个独立的 Git 子进程并等待最多 10 秒；仓库副作用完全取决于传入参数，本方法本身仅负责进程启动与输出采集。
    /// en: This method launches a separate Git subprocess and waits up to 10 seconds; any repository side effects depend entirely on the supplied arguments, while the method itself only handles process startup and stdout capture.
    /// </remarks>
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




