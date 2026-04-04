using Microsoft.AspNetCore.Mvc;
using OpenStaff.Api.Services;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// 智能体控制器 / Agents controller
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/agents")]
public class AgentsController : ControllerBase
{
    private readonly AgentService _agentService;

    public AgentsController(AgentService agentService)
    {
        _agentService = agentService;
    }

    /// <summary>获取工程角色实例列表 / Get project agent instances</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid projectId, CancellationToken cancellationToken)
    {
        var agents = await _agentService.GetProjectAgentsAsync(projectId, cancellationToken);
        return Ok(agents);
    }

    /// <summary>获取角色事件历史 / Get agent event history</summary>
    [HttpGet("{agentId:guid}/events")]
    public async Task<IActionResult> GetEvents(Guid projectId, Guid agentId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var events = await _agentService.GetAgentEventsAsync(projectId, agentId, page, pageSize, cancellationToken);
        return Ok(events);
    }

    /// <summary>向角色发送消息 / Send message to agent</summary>
    [HttpPost("{agentId:guid}/message")]
    public async Task<IActionResult> SendMessage(Guid projectId, Guid agentId,
        [FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        var response = await _agentService.SendMessageAsync(projectId, agentId, request, cancellationToken);
        return Ok(response);
    }
}
