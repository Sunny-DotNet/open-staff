using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Contracts.Agents;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/agents")]
public class AgentsController : ControllerBase
{
    private readonly IAgentAppService _agentAppService;

    public AgentsController(IAgentAppService agentAppService)
    {
        _agentAppService = agentAppService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid projectId, CancellationToken ct)
        => Ok(await _agentAppService.GetProjectAgentsAsync(projectId, ct));

    [HttpPut]
    public async Task<IActionResult> SetAgents(Guid projectId, [FromBody] SetAgentsRequest request, CancellationToken ct)
    {
        await _agentAppService.SetProjectAgentsAsync(projectId, request.AgentRoleIds, ct);
        return Ok(new { message = "ok" });
    }

    [HttpGet("{agentId:guid}/events")]
    public async Task<IActionResult> GetEvents(Guid projectId, Guid agentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _agentAppService.GetEventsAsync(projectId, agentId, page, pageSize, ct));

    [HttpPost("{agentId:guid}/message")]
    public async Task<IActionResult> SendMessage(Guid projectId, Guid agentId, [FromBody] SendAgentMessageRequest request, CancellationToken ct)
    {
        await _agentAppService.SendMessageAsync(projectId, agentId, request.Message, ct);
        return Ok(new { status = "sent" });
    }
}

public class SetAgentsRequest
{
    public List<Guid> AgentRoleIds { get; set; } = [];
}

public class SendAgentMessageRequest
{
    public string Message { get; set; } = string.Empty;
}
