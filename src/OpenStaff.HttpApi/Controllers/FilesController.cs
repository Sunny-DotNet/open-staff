using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Contracts.Files;
using OpenStaff.Application.Contracts.Files.Dtos;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/files")]
public class FilesController : ControllerBase
{
    private readonly IFileAppService _fileAppService;

    public FilesController(IFileAppService fileAppService)
    {
        _fileAppService = fileAppService;
    }

    [HttpGet]
    public async Task<IActionResult> GetFileTree(Guid projectId, CancellationToken ct)
        => Ok(await _fileAppService.GetFileTreeAsync(projectId, ct));

    [HttpGet("content")]
    public async Task<IActionResult> GetFileContent(Guid projectId, [FromQuery] string path, CancellationToken ct)
    {
        var content = await _fileAppService.GetFileContentAsync(new GetFileContentRequest { ProjectId = projectId, Path = path }, ct);
        return content == null ? NotFound() : Ok(new { content });
    }

    [HttpGet("diff")]
    public async Task<IActionResult> GetDiff(Guid projectId, [FromQuery] string? commitSha, CancellationToken ct)
    {
        var diff = await _fileAppService.GetDiffAsync(new GetDiffRequest { ProjectId = projectId, CommitSha = commitSha }, ct);
        return Ok(new { diff });
    }

    [HttpGet("~/api/projects/{projectId:guid}/checkpoints")]
    public async Task<IActionResult> GetCheckpoints(Guid projectId, CancellationToken ct)
        => Ok(await _fileAppService.GetCheckpointsAsync(projectId, ct));
}
