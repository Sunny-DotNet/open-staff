using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Contracts.Agents;
using OpenStaff.Application.Contracts.Agents.Dtos;

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
    public async Task<IActionResult> SetAgents(Guid projectId, [FromBody] SetAgentsBody body, CancellationToken ct)
    {
        await _agentAppService.SetProjectAgentsAsync(new SetProjectAgentsRequest
        {
            ProjectId = projectId,
            AgentRoleIds = body.AgentRoleIds
        }, ct);
        return Ok(new { message = "ok" });
    }

    [HttpGet("{agentId:guid}/events")]
    public async Task<IActionResult> GetEvents(Guid projectId, Guid agentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _agentAppService.GetEventsAsync(new GetAgentEventsRequest
        {
            ProjectId = projectId,
            AgentId = agentId,
            Page = page,
            PageSize = pageSize
        }, ct));

    [HttpPost("{agentId:guid}/message")]
    public async Task<IActionResult> SendMessage(Guid projectId, Guid agentId, [FromBody] SendMessageBody body, CancellationToken ct)
    {
        await _agentAppService.SendMessageAsync(new SendAgentMessageRequest
        {
            ProjectId = projectId,
            AgentId = agentId,
            Message = body.Message
        }, ct);
        return Ok(new { status = "sent" });
    }
}

public class SetAgentsBody
{
    public List<Guid> AgentRoleIds { get; set; } = [];
}

public class SendMessageBody
{
    public string Message { get; set; } = string.Empty;
}
