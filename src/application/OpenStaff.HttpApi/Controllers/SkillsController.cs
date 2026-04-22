using Microsoft.AspNetCore.Mvc;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// Skill 目录与安装管理控制器。
/// Controller for skill catalog and installation management.
/// </summary>
[ApiController]
[Route("api/skills")]
public class SkillsController : ControllerBase
{
    private readonly ISkillApiService _skillService;

    /// <summary>
    /// 初始化控制器。
    /// Initializes the controller.
    /// </summary>
    public SkillsController(ISkillApiService skillService)
    {
        _skillService = skillService;
    }

    /// <summary>
    /// 搜索 Skill 目录。
    /// Searches the skill catalog.
    /// </summary>
    [HttpGet("catalog")]
    public async Task<ActionResult<SkillCatalogPageDto>> SearchCatalog([FromQuery] SkillCatalogQueryInput input, CancellationToken ct)
        => Ok(await _skillService.SearchCatalogAsync(input, ct));

    /// <summary>
    /// 获取 Skill 来源聚合。
    /// Gets aggregated skill sources.
    /// </summary>
    [HttpGet("sources")]
    public async Task<ActionResult<List<SkillCatalogSourceDto>>> GetSources(CancellationToken ct)
        => Ok(await _skillService.GetSourcesAsync(ct));

    /// <summary>
    /// 获取单个 Skill。
    /// Gets a single skill catalog item.
    /// </summary>
    [HttpGet("catalog/{owner}/{repo}/{skillId}")]
    public async Task<ActionResult<SkillCatalogItemDto>> GetCatalogItem(string owner, string repo, string skillId, CancellationToken ct)
    {
        var result = await _skillService.GetCatalogItemAsync(owner, repo, skillId, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 获取已安装 Skill 列表。
    /// Gets installed skill records.
    /// </summary>
    [HttpGet("installed")]
    public async Task<ActionResult<List<InstalledSkillDto>>> GetInstalled([FromQuery] GetInstalledSkillsInput input, CancellationToken ct)
        => Ok(await _skillService.GetInstalledAsync(input, ct));

    /// <summary>
    /// 安装 Skill。
    /// Installs a skill.
    /// </summary>
    [HttpPost("install")]
    public async Task<ActionResult<InstalledSkillDto>> Install([FromBody] InstallSkillInput input, CancellationToken ct)
        => Ok(await _skillService.InstallAsync(input, ct));

    /// <summary>
    /// 卸载 Skill。
    /// Uninstalls a skill.
    /// </summary>
    [HttpPost("uninstall")]
    public async Task<ActionResult<bool>> Uninstall([FromBody] UninstallSkillInput input, CancellationToken ct)
        => Ok(await _skillService.UninstallAsync(input, ct));

    /// <summary>
    /// 获取测试角色 Skill 绑定。
    /// Gets role-level skill bindings for test chat.
    /// </summary>
    [HttpGet("agent-bindings/{agentRoleId:guid}")]
    public async Task<ActionResult<List<AgentRoleSkillBindingDto>>> GetAgentRoleBindings(Guid agentRoleId, CancellationToken ct)
        => Ok(await _skillService.GetAgentRoleBindingsAsync(agentRoleId, ct));

    /// <summary>
    /// 替换测试角色 Skill 绑定。
    /// Replaces role-level skill bindings for test chat.
    /// </summary>
    [HttpPut("agent-bindings/{agentRoleId:guid}")]
    public async Task<IActionResult> ReplaceAgentRoleBindings(
        Guid agentRoleId,
        [FromBody] List<AgentRoleSkillBindingInput>? bindings,
        CancellationToken ct)
    {
        await _skillService.ReplaceAgentRoleBindingsAsync(new ReplaceAgentRoleSkillBindingsRequest
        {
            AgentRoleId = agentRoleId,
            Bindings = bindings ?? []
        }, ct);

        return NoContent();
    }
}

