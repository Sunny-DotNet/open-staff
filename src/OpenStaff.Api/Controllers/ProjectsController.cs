using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenStaff.Api.Services;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// 工程管理控制器 / Project management controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(ProjectService projectService, ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    /// <summary>获取工程列表 / Get project list</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var projects = await _projectService.GetAllAsync(cancellationToken);
        return Ok(projects);
    }

    /// <summary>获取工程详情 / Get project detail</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projectService.GetByIdAsync(id, cancellationToken);
        if (project == null) return NotFound();
        return Ok(project);
    }

    /// <summary>创建工程 / Create project</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await _projectService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    /// <summary>更新工程 / Update project</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await _projectService.UpdateAsync(id, request, cancellationToken);
        if (project == null) return NotFound();
        return Ok(project);
    }

    /// <summary>删除工程 / Delete project</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.DeleteAsync(id, cancellationToken);
        if (!result) return NotFound();
        return NoContent();
    }

    /// <summary>初始化工程 / Initialize project (start communicator)</summary>
    [HttpPost("{id:guid}/initialize")]
    public async Task<IActionResult> Initialize(Guid id, CancellationToken cancellationToken)
    {
        await _projectService.InitializeAsync(id, cancellationToken);
        return Ok(new { message = "工程初始化已启动 / Project initialization started" });
    }

    /// <summary>导出工程 / Export project</summary>
    [HttpPost("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var filePath = await _projectService.ExportAsync(id, cancellationToken);

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogError("Export file not found: {FilePath}", filePath);
                return NotFound(new { error = "导出文件未找到 / Export file not found" });
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath, cancellationToken);
            var fileName = Path.GetFileName(filePath);

            _logger.LogInformation("Project {ProjectId} exported successfully to {FilePath}", id, filePath);
            return File(fileBytes, "application/octet-stream", fileName);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during project export: {ProjectId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting project {ProjectId}", id);
            return StatusCode(500, new { error = "导出失败 / Export failed" });
        }
    }

    /// <summary>导入工程 / Import project</summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("Import attempt with empty file");
                return BadRequest(new { error = "文件为空 / File is empty" });
            }

            // 验证文件扩展名
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".openstaff")
            {
                _logger.LogWarning("Invalid file extension for import: {Extension}", extension);
                return BadRequest(new { error = "不支持的文件格式 / Unsupported file format" });
            }

            // 验证文件大小 (限制为100MB)
            const int maxFileSize = 100 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                _logger.LogWarning("File too large for import: {FileSize} bytes", file.Length);
                return BadRequest(new { error = "文件过大 / File too large" });
            }

            var projectId = await _projectService.ImportAsync(file, cancellationToken);
            _logger.LogInformation("Project imported successfully: {ProjectId}", projectId);
            return Ok(new { projectId, message = "工程已导入 / Project imported" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during project import");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing project from file: {FileName}", file?.FileName);
            return StatusCode(500, new { error = "导入失败 / Import failed" });
        }
    }
}
