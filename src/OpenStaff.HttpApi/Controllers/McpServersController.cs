using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Contracts.McpServers;
using OpenStaff.Application.Contracts.McpServers.Dtos;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/mcp")]
public class McpServersController : ControllerBase
{
    private readonly IMcpServerAppService _mcpService;

    public McpServersController(IMcpServerAppService mcpService)
    {
        _mcpService = mcpService;
    }

    #region MCP Server 定义（市场）

    [HttpGet("servers")]
    public async Task<IActionResult> GetAllServers([FromQuery] string? category, [FromQuery] string? search, CancellationToken ct)
        => Ok(await _mcpService.GetAllServersAsync(new GetAllServersRequest { Category = category, Search = search }, ct));

    [HttpGet("servers/{id:guid}")]
    public async Task<IActionResult> GetServerById(Guid id, CancellationToken ct)
    {
        var result = await _mcpService.GetServerByIdAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("servers")]
    public async Task<IActionResult> CreateServer([FromBody] CreateMcpServerInput input, CancellationToken ct)
        => Ok(await _mcpService.CreateServerAsync(input, ct));

    [HttpDelete("servers/{id:guid}")]
    public async Task<IActionResult> DeleteServer(Guid id, CancellationToken ct)
        => await _mcpService.DeleteServerAsync(id, ct) ? NoContent() : NotFound();

    #endregion

    #region MCP 配置实例

    [HttpGet("servers/{mcpServerId:guid}/configs")]
    public async Task<IActionResult> GetConfigsByServer(Guid mcpServerId, CancellationToken ct)
        => Ok(await _mcpService.GetConfigsByServerAsync(mcpServerId, ct));

    [HttpGet("configs")]
    public async Task<IActionResult> GetAllConfigs(CancellationToken ct)
        => Ok(await _mcpService.GetAllConfigsAsync(ct));

    [HttpGet("configs/{id:guid}")]
    public async Task<IActionResult> GetConfigById(Guid id, CancellationToken ct)
    {
        var result = await _mcpService.GetConfigByIdAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("configs")]
    public async Task<IActionResult> CreateConfig([FromBody] CreateMcpServerConfigInput input, CancellationToken ct)
        => Ok(await _mcpService.CreateConfigAsync(input, ct));

    [HttpPut("configs/{id:guid}")]
    public async Task<IActionResult> UpdateConfig(Guid id, [FromBody] UpdateMcpServerConfigInput input, CancellationToken ct)
    {
        var result = await _mcpService.UpdateConfigAsync(id, input, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("configs/{id:guid}")]
    public async Task<IActionResult> DeleteConfig(Guid id, CancellationToken ct)
        => await _mcpService.DeleteConfigAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("configs/{id:guid}/test")]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken ct)
        => Ok(await _mcpService.TestConnectionAsync(id, ct));

    #endregion

    #region 员工 MCP 绑定

    [HttpGet("agent-bindings/{agentRoleId:guid}")]
    public async Task<IActionResult> GetAgentBindings(Guid agentRoleId, CancellationToken ct)
        => Ok(await _mcpService.GetAgentBindingsAsync(agentRoleId, ct));

    [HttpPut("agent-bindings/{agentRoleId:guid}")]
    public async Task<IActionResult> SetAgentBindings(Guid agentRoleId, [FromBody] List<Guid> mcpServerConfigIds, CancellationToken ct)
    {
        await _mcpService.SetAgentBindingsAsync(new SetAgentBindingsRequest
        {
            AgentRoleId = agentRoleId,
            McpServerConfigIds = mcpServerConfigIds
        }, ct);
        return NoContent();
    }

    #endregion
}
