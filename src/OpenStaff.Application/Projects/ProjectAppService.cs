using Microsoft.AspNetCore.Http;
using OpenStaff.Application.Contracts.Projects;
using OpenStaff.Application.Contracts.Projects.Dtos;
using OpenStaff.Core.Models;

namespace OpenStaff.Application.Projects;

public class ProjectAppService : IProjectAppService
{
    private readonly ProjectService _projectService;

    public ProjectAppService(ProjectService projectService)
    {
        _projectService = projectService;
    }

    public async Task<List<ProjectDto>> GetAllAsync(CancellationToken ct)
    {
        var projects = await _projectService.GetAllAsync(ct);
        return projects.Select(MapToDto).ToList();
    }

    public async Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(id, ct);
        return project == null ? null : MapToDto(project);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectInput input, CancellationToken ct)
    {
        var request = new CreateProjectRequest
        {
            Name = input.Name,
            Description = input.Description
        };
        var project = await _projectService.CreateAsync(request, ct);
        return MapToDto(project);
    }

    public async Task<ProjectDto?> UpdateAsync(Guid id, UpdateProjectInput input, CancellationToken ct)
    {
        var request = new UpdateProjectRequest
        {
            Name = input.Name,
            Description = input.Description
        };
        var project = await _projectService.UpdateAsync(id, request, ct);
        return project == null ? null : MapToDto(project);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        return await _projectService.DeleteAsync(id, ct);
    }

    public async Task<ProjectDto?> InitializeAsync(Guid id, CancellationToken ct)
    {
        await _projectService.InitializeAsync(id, ct);
        var project = await _projectService.GetByIdAsync(id, ct);
        return project == null ? null : MapToDto(project);
    }

    public async Task<string?> ExportAsync(Guid id, CancellationToken ct)
    {
        return await _projectService.ExportAsync(id, ct);
    }

    public async Task<ProjectDto?> ImportAsync(Stream fileStream, string fileName, CancellationToken ct)
    {
        // ProjectService.ImportAsync expects IFormFile, so we need to create a wrapper
        var formFile = new StreamFormFile(fileStream, fileName);
        var projectId = await _projectService.ImportAsync(formFile, ct);
        var project = await _projectService.GetByIdAsync(projectId, ct);
        return project == null ? null : MapToDto(project);
    }

    private static ProjectDto MapToDto(Project p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        WorkspacePath = p.WorkspacePath,
        Status = p.Status ?? ProjectStatus.Initializing,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };

    /// <summary>
    /// IFormFile wrapper for a plain Stream
    /// </summary>
    private sealed class StreamFormFile : IFormFile
    {
        private readonly Stream _stream;
        public StreamFormFile(Stream stream, string fileName)
        {
            _stream = stream;
            FileName = fileName;
            Name = "file";
            Length = stream.CanSeek ? stream.Length : 0;
        }
        public string ContentType => "application/octet-stream";
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length { get; }
        public string Name { get; }
        public string FileName { get; }
        public Stream OpenReadStream() => _stream;
        public void CopyTo(Stream target) => _stream.CopyTo(target);
        public Task CopyToAsync(Stream target, CancellationToken ct = default) => _stream.CopyToAsync(target, ct);
    }
}
