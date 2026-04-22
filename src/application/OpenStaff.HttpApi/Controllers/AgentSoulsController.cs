using Microsoft.AspNetCore.Mvc;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// 灵魂配置目录控制器。
/// Controller for the agent-soul option catalog.
/// </summary>
[ApiController]
[Route("api/agent-souls")]
public class AgentSoulsController : ControllerBase
{
    private readonly IAgentSoulApiService _agentSoulService;

    public AgentSoulsController(IAgentSoulApiService agentSoulService)
    {
        _agentSoulService = agentSoulService;
    }

    /// <summary>
    /// 获取灵魂配置选项。
    /// Gets the grouped soul-option catalog.
    /// </summary>
    [HttpGet("options")]
    public async Task<ActionResult<AgentSoulCatalogDto>> GetOptions([FromQuery] string? locale, CancellationToken ct)
        => Ok(await _agentSoulService.GetOptionsAsync(locale, ct));
}
