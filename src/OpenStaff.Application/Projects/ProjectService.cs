using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Export;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Application.Projects;

/// <summary>
/// 工程应用服务 / Project application service
/// </summary>
public class ProjectService
{
    private readonly AppDbContext _db;
    private readonly ProjectExporter _exporter;
    private readonly ProjectImporter _importer;
    private readonly IConfiguration _config;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(AppDbContext db, ProjectExporter exporter, ProjectImporter importer, IConfiguration config, ILogger<ProjectService> logger)
    {
        _db = db;
        _exporter = exporter;
        _importer = importer;
        _config = config;
        _logger = logger;
    }

    public async Task<List<Project>> GetAllAsync(CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogDebug("Fetching all projects");

            var projects = await _db.Projects
                .OrderByDescending(p => p.UpdatedAt)
                .Include(p => p.Agents)
                .ToListAsync(ct);

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("Retrieved {Count} projects in {ElapsedMs}ms", projects.Count, elapsed);

            return projects;
        }
        catch (Exception ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Error fetching all projects after {ElapsedMs}ms", elapsed);
            throw;
        }
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogDebug("Fetching project {ProjectId}", id);

            var project = await _db.Projects
                .Include(p => p.Agents)
                .ThenInclude(a => a.AgentRole)
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            if (project != null)
            {
                _logger.LogInformation("Retrieved project {ProjectId} in {ElapsedMs}ms", id, elapsed);
            }
            else
            {
                _logger.LogWarning("Project {ProjectId} not found", id);
            }

            return project;
        }
        catch (Exception ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Error fetching project {ProjectId} after {ElapsedMs}ms", id, elapsed);
            throw;
        }
    }

    public async Task<Project> CreateAsync(CreateProjectRequest request, CancellationToken ct)
    {
        var project = new Project
        {
            Name = request.Name,
            Description = request.Description,
            Language = request.Language ?? "zh-CN"
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);
        return project;
    }

    public async Task<Project?> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { id }, ct);
        if (project == null) return null;

        if (request.Name != null) project.Name = request.Name;
        if (request.Description != null) project.Description = request.Description;
        if (request.Language != null) project.Language = request.Language;
        if (request.DefaultProviderId.HasValue) project.DefaultProviderId = request.DefaultProviderId;
        if (request.DefaultModelName != null) project.DefaultModelName = request.DefaultModelName;
        if (request.ExtraConfig != null) project.ExtraConfig = request.ExtraConfig;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return project;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { id }, ct);
        if (project == null) return false;

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task InitializeAsync(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects
            .Include(p => p.MainSession)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project == null)
            throw new InvalidOperationException($"Project {id} not found");

        if (project.Status != ProjectStatus.Initializing && project.Status != ProjectStatus.Paused)
            throw new InvalidOperationException($"Project {id} is in {project.Status} status, cannot initialize");

        // 1. 创建工作空间目录
        var workspacesRoot = _config["Workspaces:RootPath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".staff", "workspaces");
        var workspacePath = Path.Combine(workspacesRoot, project.Id.ToString("N"));
        Directory.CreateDirectory(workspacePath);
        project.WorkspacePath = workspacePath;

        // 2. Git init（如果 git 可用）
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", "init")
            {
                WorkingDirectory = workspacePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync(ct);
                _logger.LogInformation("Git initialized in {Path}", workspacePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Git init failed for project {ProjectId}, continuing without git", id);
        }

        // 3. 创建主群聊 Session
        if (project.MainSessionId == null)
        {
            var session = new ChatSession
            {
                ProjectId = project.Id,
                InitialInput = $"项目「{project.Name}」群聊已创建",
                ContextStrategy = ContextStrategies.Full
            };
            _db.ChatSessions.Add(session);
            project.MainSessionId = session.Id;
        }

        // 4. 更新项目状态
        project.Status = ProjectStatus.Active;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Project {ProjectId} ({Name}) initialized: workspace={Path}, session={SessionId}",
            id, project.Name, workspacePath, project.MainSessionId);
    }

    public async Task<string> ExportAsync(Guid id, CancellationToken ct)
    {
        var exportBasePath = _config["FileStorage:ExportPath"];
        var exportPath = string.IsNullOrEmpty(exportBasePath)
            ? Path.Combine(Path.GetTempPath(), "openstaff-exports")
            : Path.IsPathRooted(exportBasePath)
                ? exportBasePath
                : Path.Combine(Directory.GetCurrentDirectory(), exportBasePath);

        Directory.CreateDirectory(exportPath);
        _logger.LogInformation("Exporting project {ProjectId} to {ExportPath}", id, exportPath);
        return await _exporter.ExportAsync(id, exportPath, ct);
    }

    public async Task<Guid> ImportAsync(IFormFile file, CancellationToken ct)
    {
        var tempBasePath = _config["FileStorage:TempPath"];
        var tempDir = string.IsNullOrEmpty(tempBasePath)
            ? Path.GetTempPath()
            : Path.IsPathRooted(tempBasePath)
                ? tempBasePath
                : Path.Combine(Directory.GetCurrentDirectory(), tempBasePath);

        Directory.CreateDirectory(tempDir);

        var tempFile = Path.Combine(tempDir, $"import-{Guid.NewGuid()}.openstaff");
        _logger.LogInformation("Importing project from {FileName} to {TempFile}", file.FileName, tempFile);

        using (var stream = new FileStream(tempFile, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }

        var workspacesRoot = _config["Workspaces:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "workspaces");
        Directory.CreateDirectory(workspacesRoot);

        return await _importer.ImportAsync(tempFile, workspacesRoot, ct);
    }

    /// <summary>获取项目的员工列表（含角色详情）</summary>
    public async Task<List<ProjectAgent>> GetProjectAgentsAsync(Guid projectId, CancellationToken ct)
    {
        return await _db.ProjectAgents
            .Where(pa => pa.ProjectId == projectId)
            .Include(pa => pa.AgentRole)
            .OrderBy(pa => pa.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>批量设置项目参与的员工（全量替换）</summary>
    public async Task SetProjectAgentsAsync(Guid projectId, List<Guid> agentRoleIds, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) throw new InvalidOperationException($"Project {projectId} not found");

        // 移除现有的
        var existing = await _db.ProjectAgents.Where(pa => pa.ProjectId == projectId).ToListAsync(ct);
        _db.ProjectAgents.RemoveRange(existing);

        // 添加新的
        foreach (var roleId in agentRoleIds.Distinct())
        {
            _db.ProjectAgents.Add(new ProjectAgent
            {
                ProjectId = projectId,
                AgentRoleId = roleId
            });
        }

        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Project {ProjectId} agents updated: {Count} roles", projectId, agentRoleIds.Count);
    }
}

// 请求模型 / Request models
public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Language { get; set; }
}

public class UpdateProjectRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Language { get; set; }
    public Guid? DefaultProviderId { get; set; }
    public string? DefaultModelName { get; set; }
    public string? ExtraConfig { get; set; }
}
