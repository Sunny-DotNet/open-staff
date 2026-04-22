using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// 项目应用服务契约。
/// Application service contract for project lifecycle operations.
/// </summary>
public interface IProjectApiService : IApiServiceBase
{
    /// <summary>
    /// 获取所有项目。
    /// Gets all projects.
    /// </summary>
    Task<List<ProjectDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// 根据标识获取项目。
    /// Gets a project by identifier.
    /// </summary>
    Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 创建项目。
    /// Creates a project.
    /// </summary>
    Task<ProjectDto> CreateAsync(CreateProjectInput input, CancellationToken ct = default);

    /// <summary>
    /// 更新项目设置。
    /// Updates project settings.
    /// </summary>
    Task<ProjectDto?> UpdateAsync(Guid id, UpdateProjectInput input, CancellationToken ct = default);

    /// <summary>
    /// 删除项目。
    /// Deletes a project.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 初始化项目工作区和初始资源。
    /// Initializes the project workspace and initial resources.
    /// </summary>
    Task<ProjectDto?> InitializeAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 启动项目运行态。
    /// Starts the project runtime state.
    /// </summary>
    Task<ProjectDto?> StartAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 获取项目 README 内容。
    /// Gets the project README content.
    /// </summary>
    Task<string?> GetReadmeAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 导出项目并返回生成包路径。
    /// Exports a project and returns the generated package path.
    /// </summary>
    Task<string?> ExportAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 从导出包导入项目。
    /// Imports a project from an exported package.
    /// </summary>
    Task<ProjectDto?> ImportAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}


