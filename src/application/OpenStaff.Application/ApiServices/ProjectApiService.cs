using OpenStaff.Application.Projects.Services;

namespace OpenStaff.ApiServices;
/// <summary>
/// 项目应用服务实现。
/// Application service implementation for project lifecycle operations.
/// </summary>
public class ProjectApiService : ApiServiceBase, IProjectApiService
{
    private readonly ProjectService _projectService;

    /// <summary>
    /// 初始化项目应用服务。
    /// Initializes the project application service.
    /// </summary>
    public ProjectApiService(ProjectService projectService, IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _projectService = projectService;
    }

    /// <inheritdoc />
    public async Task<List<ProjectDto>> GetAllAsync(CancellationToken ct)
    {
        var projects = await _projectService.GetAllAsync(ct);
        return projects.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(id, ct);
        return project == null ? null : MapToDto(project);
    }

    /// <inheritdoc />
    public async Task<ProjectDto> CreateAsync(CreateProjectInput input, CancellationToken ct)
    {
        var request = new CreateProjectRequest
        {
            Name = input.Name,
            Description = input.Description,
            Language = input.Language,
            DefaultProviderId = input.DefaultProviderId,
            DefaultModelName = input.DefaultModelName,
        };
        var project = await _projectService.CreateAsync(request, ct);
        return MapToDto(project);
    }

    /// <inheritdoc />
    public async Task<ProjectDto?> UpdateAsync(Guid id, UpdateProjectInput input, CancellationToken ct)
    {
        var request = new UpdateProjectRequest
        {
            Name = input.Name,
            Description = input.Description,
            Language = input.Language,
            DefaultProviderId = input.DefaultProviderId.HasValue ? input.DefaultProviderId : null,
            DefaultModelName = input.DefaultModelName,
            ExtraConfig = input.ExtraConfig,
        };
        var project = await _projectService.UpdateAsync(id, request, ct);
        return project == null ? null : MapToDto(project);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        return await _projectService.DeleteAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task<ProjectDto?> InitializeAsync(Guid id, CancellationToken ct)
    {
        await _projectService.InitializeAsync(id, ct);
        var project = await _projectService.GetByIdAsync(id, ct);
        return project == null ? null : MapToDto(project);
    }

    /// <inheritdoc />
    public async Task<ProjectDto?> StartAsync(Guid id, CancellationToken ct)
    {
        var project = await _projectService.StartAsync(id, ct);
        return MapToDto(project);
    }

    /// <inheritdoc />
    public async Task<string?> ExportAsync(Guid id, CancellationToken ct)
    {
        return await _projectService.ExportAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task<ProjectDto?> ImportAsync(Stream fileStream, string fileName, CancellationToken ct)
    {
        // ProjectService.ImportAsync expects IFormFile, so we need to create a wrapper
        var formFile = new StreamFormFile(fileStream, fileName);
        var projectId = await _projectService.ImportAsync(formFile, ct);
        var project = await _projectService.GetByIdAsync(projectId, ct);
        return project == null ? null : MapToDto(project);
    }

    /// <inheritdoc />
    public async Task<string?> GetReadmeAsync(Guid id, CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(id, ct);
        if (project?.WorkspacePath == null) return null;
        var readmePath = Path.Combine(project.WorkspacePath, ".staff", "project-brainstorm.md");
        if (!File.Exists(readmePath)) return string.Empty;
        return await File.ReadAllTextAsync(readmePath, ct);
    }

    /// <summary>
    /// 将项目实体映射为应用层传输对象。
    /// Maps a project entity to the application-layer DTO.
    /// </summary>
    /// <param name="p">项目实体。/ Project entity.</param>
    /// <returns>供 API 返回的项目 DTO。/ The project DTO returned by the API.</returns>
    /// <remarks>
    /// zh-CN: 该方法只做字段投影与默认值补齐，不读取工作区文件，也不会修改项目状态。
    /// en: This method performs field projection and default-value normalization only; it does not read workspace files or mutate project state.
    /// </remarks>
    private static ProjectDto MapToDto(Project p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        WorkspacePath = p.WorkspacePath,
        Status = p.Status ?? ProjectStatus.Initializing,
        Phase = p.Phase ?? ProjectPhases.Brainstorming,
        Language = p.Language ?? "zh-CN",
        DefaultProviderId = p.DefaultProviderId,
        DefaultModelName = p.DefaultModelName,
        ExtraConfig = p.ExtraConfig,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };

    /// <summary>
    /// IFormFile wrapper for a plain Stream
    /// </summary>
    private sealed class StreamFormFile : IFormFile
    {
        private readonly Stream _stream;
        /// <inheritdoc />
        public StreamFormFile(Stream stream, string fileName)
        {
            _stream = stream;
            FileName = fileName;
            Name = "file";
            Length = stream.CanSeek ? stream.Length : 0;
        }
        public string ContentType => "application/octet-stream";
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
        /// <inheritdoc />
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length { get; }
        public string Name { get; }
        public string FileName { get; }
        /// <inheritdoc />
        public Stream OpenReadStream() => _stream;
        /// <inheritdoc />
        public void CopyTo(Stream target) => _stream.CopyTo(target);
        /// <inheritdoc />
        public Task CopyToAsync(Stream target, CancellationToken ct = default) => _stream.CopyToAsync(target, ct);
    }
}



