using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Export;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Api.Services;

/// <summary>
/// 工程应用服务 / Project application service
/// </summary>
public class ProjectService
{
    private readonly AppDbContext _db;
    private readonly ProjectExporter _exporter;
    private readonly ProjectImporter _importer;
    private readonly IConfiguration _config;

    public ProjectService(AppDbContext db, ProjectExporter exporter, ProjectImporter importer, IConfiguration config)
    {
        _db = db;
        _exporter = exporter;
        _importer = importer;
        _config = config;
    }

    public async Task<List<Project>> GetAllAsync(CancellationToken ct)
    {
        return await _db.Projects
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.Projects
            .Include(p => p.Agents).ThenInclude(a => a.AgentRole)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Project> CreateAsync(CreateProjectRequest request, CancellationToken ct)
    {
        var project = new Project
        {
            Name = request.Name,
            Description = request.Description,
            TechStack = request.TechStack,
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
        if (request.TechStack != null) project.TechStack = request.TechStack;
        if (request.Language != null) project.Language = request.Language;
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

    public Task InitializeAsync(Guid id, CancellationToken ct)
    {
        // TODO: 启动对话者进行交互式初始化
        return Task.CompletedTask;
    }

    public async Task<string> ExportAsync(Guid id, CancellationToken ct)
    {
        var exportPath = Path.Combine(Path.GetTempPath(), "openstaff-exports");
        Directory.CreateDirectory(exportPath);
        return await _exporter.ExportAsync(id, exportPath, ct);
    }

    public async Task<Guid> ImportAsync(IFormFile file, CancellationToken ct)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"import-{Guid.NewGuid()}.openstaff");
        using (var stream = new FileStream(tempFile, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }

        var workspacesRoot = _config["WorkspacesRoot"] ?? Path.Combine(Directory.GetCurrentDirectory(), "workspaces");
        return await _importer.ImportAsync(tempFile, workspacesRoot, ct);
    }
}

// 请求模型 / Request models
public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? TechStack { get; set; }
    public string? Language { get; set; }
}

public class UpdateProjectRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? TechStack { get; set; }
    public string? Language { get; set; }
}
