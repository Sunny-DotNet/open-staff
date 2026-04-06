using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Contracts.Projects;
using OpenStaff.Application.Contracts.Projects.Dtos;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectAppService _projectAppService;

    public ProjectsController(IProjectAppService projectAppService)
    {
        _projectAppService = projectAppService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _projectAppService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _projectAppService.GetByIdAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectInput input, CancellationToken ct)
    {
        var result = await _projectAppService.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectInput input, CancellationToken ct)
    {
        var result = await _projectAppService.UpdateAsync(id, input, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _projectAppService.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/initialize")]
    public async Task<IActionResult> Initialize(Guid id, CancellationToken ct)
    {
        var result = await _projectAppService.InitializeAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        var filePath = await _projectAppService.ExportAsync(id, ct);
        if (filePath == null || !System.IO.File.Exists(filePath))
            return NotFound(new { message = "导出失败" });

        var stream = System.IO.File.OpenRead(filePath);
        return File(stream, "application/octet-stream", Path.GetFileName(filePath));
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "请选择文件" });

        if (!file.FileName.EndsWith(".openstaff", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "仅支持 .openstaff 格式" });

        if (file.Length > 100 * 1024 * 1024)
            return BadRequest(new { message = "文件大小不能超过 100MB" });

        await using var stream = file.OpenReadStream();
        var result = await _projectAppService.ImportAsync(stream, file.FileName, ct);
        return result == null ? BadRequest(new { message = "导入失败" }) : Ok(result);
    }
}
