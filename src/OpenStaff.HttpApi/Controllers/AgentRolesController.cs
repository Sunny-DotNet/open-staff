using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Contracts.AgentRoles;
using OpenStaff.Application.Contracts.AgentRoles.Dtos;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/agent-roles")]
public class AgentRolesController : ControllerBase
{
    private readonly IAgentRoleAppService _agentRoleAppService;

    public AgentRolesController(IAgentRoleAppService agentRoleAppService)
    {
        _agentRoleAppService = agentRoleAppService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _agentRoleAppService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _agentRoleAppService.GetByIdAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAgentRoleInput input, CancellationToken ct)
        => Ok(await _agentRoleAppService.CreateAsync(input, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAgentRoleInput input, CancellationToken ct)
    {
        var result = await _agentRoleAppService.UpdateAsync(id, input, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _agentRoleAppService.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/test-chat")]
    public async Task<IActionResult> TestChat(Guid id, [FromBody] TestChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "消息不能为空" });

        var sessionId = await _agentRoleAppService.TestChatAsync(id, request.Message, ct);
        return Ok(new { sessionId });
    }
}

public class TestChatRequest
{
    public string Message { get; set; } = string.Empty;
}
