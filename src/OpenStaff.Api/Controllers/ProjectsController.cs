using Microsoft.AspNetCore.Mvc;
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

    public ProjectsController(ProjectService projectService)
    {
        _projectService = projectService;
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
        var filePath = await _projectService.ExportAsync(id, cancellationToken);
        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath, cancellationToken);
        return File(fileBytes, "application/octet-stream", Path.GetFileName(filePath));
    }

    /// <summary>导入工程 / Import project</summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken cancellationToken)
    {
        var projectId = await _projectService.ImportAsync(file, cancellationToken);
        return Ok(new { projectId, message = "工程已导入 / Project imported" });
    }
}
