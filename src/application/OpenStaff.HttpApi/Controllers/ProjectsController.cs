
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// 项目管理控制器。
/// Controller that exposes project management endpoints.
/// </summary>
[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectApiService _projectApiService;

    /// <summary>
    /// 初始化项目管理控制器。
    /// Initializes the project management controller.
    /// </summary>
    public ProjectsController(IProjectApiService projectApiService)
    {
        _projectApiService = projectApiService;
    }

    /// <summary>
    /// 获取所有项目。
    /// Gets all projects.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ProjectDto>>> GetAll(CancellationToken ct)
        => Ok(await _projectApiService.GetAllAsync(ct));

    /// <summary>
    /// 获取单个项目详情。
    /// Gets the details of a single project.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _projectApiService.GetByIdAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 创建项目。
    /// Creates a project.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectDto>> Create([FromBody] CreateProjectInput input, CancellationToken ct)
    {
        var result = await _projectApiService.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// 更新项目。
    /// Updates a project.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(Guid id, [FromBody] UpdateProjectInput input, CancellationToken ct)
    {
        var result = await _projectApiService.UpdateAsync(id, input, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 删除项目。
    /// Deletes a project.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _projectApiService.DeleteAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>
    /// 获取项目 README 内容。
    /// Gets the project README content.
    /// </summary>
    [HttpGet("{id:guid}/readme")]
    public async Task<ActionResult<ContentDto>> GetReadme(Guid id, CancellationToken ct)
    {
        var content = await _projectApiService.GetReadmeAsync(id, ct);
        if (content == null)
            return NotFound();

        return Ok(new ContentDto { Content = content });
    }

    /// <summary>
    /// 初始化项目工作区。
    /// Initializes the project workspace.
    /// </summary>
    [HttpPost("{id:guid}/initialize")]
    public async Task<ActionResult<ProjectDto>> Initialize(Guid id, CancellationToken ct)
    {
        var result = await _projectApiService.InitializeAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 启动项目运行态。
    /// Starts the project runtime state.
    /// </summary>
    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<ProjectDto>> Start(Guid id, CancellationToken ct)
    {
        var result = await _projectApiService.StartAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 导出项目包。
    /// Exports the project as a package.
    /// </summary>
    [HttpPost("{id:guid}/export")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        var filePath = await _projectApiService.ExportAsync(id, ct);
        if (filePath == null || !System.IO.File.Exists(filePath))
            return NotFound(new ApiMessageDto { Message = "导出失败" });

        var stream = System.IO.File.OpenRead(filePath);
        return File(stream, "application/octet-stream", Path.GetFileName(filePath));
    }

    /// <summary>
    /// 从导出包导入项目。
    /// Imports a project from an exported package.
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<ProjectDto>> Import(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ApiMessageDto { Message = "请选择文件" });

        if (!file.FileName.EndsWith(".openstaff", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ApiMessageDto { Message = "仅支持 .openstaff 格式" });

        if (file.Length > 100 * 1024 * 1024)
            return BadRequest(new ApiMessageDto { Message = "文件大小不能超过 100MB" });

        await using var stream = file.OpenReadStream();
        var result = await _projectApiService.ImportAsync(stream, file.FileName, ct);
        return result == null ? BadRequest(new ApiMessageDto { Message = "导入失败" }) : Ok(result);
    }
}

