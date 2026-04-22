
using Microsoft.AspNetCore.Mvc;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// 项目文件控制器。
/// Controller that exposes project file browsing endpoints.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/files")]
public class FilesController : ControllerBase
{
    private readonly IFileApiService _fileApiService;

    /// <summary>
    /// 初始化项目文件控制器。
    /// Initializes the project files controller.
    /// </summary>
    public FilesController(IFileApiService fileApiService)
    {
        _fileApiService = fileApiService;
    }

    /// <summary>
    /// 获取项目文件树。
    /// Gets the project file tree.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<FileNodeDto>>> GetFileTree(Guid projectId, CancellationToken ct)
        => Ok(await _fileApiService.GetFileTreeAsync(projectId, ct));

    /// <summary>
    /// 获取文件内容。
    /// Gets file content.
    /// </summary>
    [HttpGet("content")]
    public async Task<ActionResult<ContentDto>> GetFileContent(Guid projectId, [FromQuery] string path, CancellationToken ct)
    {
        var content = await _fileApiService.GetFileContentAsync(new GetFileContentRequest { ProjectId = projectId, Path = path }, ct);
        return content == null ? NotFound() : Ok(new ContentDto { Content = content });
    }

    /// <summary>
    /// 获取差异内容。
    /// Gets diff content.
    /// </summary>
    [HttpGet("diff")]
    public async Task<ActionResult<DiffDto>> GetDiff(Guid projectId, [FromQuery] string? commitSha, CancellationToken ct)
    {
        var diff = await _fileApiService.GetDiffAsync(new GetDiffRequest { ProjectId = projectId, CommitSha = commitSha }, ct);
        return Ok(new DiffDto { Diff = diff });
    }

    /// <summary>
    /// 获取项目检查点列表。
    /// Gets the checkpoints for a project.
    /// </summary>
    [HttpGet("~/api/projects/{projectId:guid}/checkpoints")]
    public async Task<ActionResult<List<CheckpointDto>>> GetCheckpoints(Guid projectId, CancellationToken ct)
        => Ok(await _fileApiService.GetCheckpointsAsync(projectId, ct));
}

