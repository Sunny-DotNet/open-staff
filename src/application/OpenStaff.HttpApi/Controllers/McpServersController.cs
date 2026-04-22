
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// MCP 服务与配置管理控制器。
/// Controller that exposes MCP server, configuration, and binding management endpoints.
/// </summary>
[ApiController]
[Route("api/mcp")]
public class McpServersController : ControllerBase
{
    private readonly IMcpApiService _mcpService;

    /// <summary>
    /// 初始化 MCP 服务控制器。
    /// Initializes the MCP server controller.
    /// </summary>
    public McpServersController(IMcpApiService mcpService)
    {
        _mcpService = mcpService;
    }

    /// <summary>
    /// 获取所有 MCP 目录源。
    /// Gets all MCP catalog sources.
    /// </summary>
    [HttpGet("sources")]
    public async Task<ActionResult<List<McpSourceDto>>> GetSources(CancellationToken ct)
        => Ok(await _mcpService.GetSourcesAsync(ct));

    /// <summary>
    /// 搜索 MCP 目录。
    /// Searches the MCP catalog.
    /// </summary>
    [HttpGet("catalog/search")]
    public async Task<ActionResult<McpCatalogSearchResultDto>> SearchCatalog([FromQuery] McpCatalogSearchQueryDto query, CancellationToken ct)
        => Ok(await _mcpService.SearchCatalogAsync(query, ct));

    /// <summary>
    /// 获取 MCP 目录条目详情。
    /// Gets a single MCP catalog entry.
    /// </summary>
    [HttpGet("catalog/entry")]
    public async Task<ActionResult<McpCatalogEntryDto>> GetCatalogEntry([FromQuery] string sourceKey, [FromQuery] string entryId, CancellationToken ct)
    {
        var result = await _mcpService.GetCatalogEntryAsync(sourceKey, entryId, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 从 MCP 目录安装服务。
    /// Installs a server from the MCP catalog.
    /// </summary>
    [HttpPost("install")]
    public async Task<ActionResult<McpServerDto>> Install([FromBody] InstallMcpServerInput input, CancellationToken ct)
        => Ok(await _mcpService.InstallAsync(input, ct));

    /// <summary>
    /// 获取 MCP 服务定义列表。
    /// Gets the list of MCP server definitions.
    /// </summary>
    [HttpGet("servers")]
    public async Task<ActionResult<List<McpServerDto>>> GetAllServers(
        [FromQuery] string? source,
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] bool? enabledState,
        [FromQuery] string? installedState,
        CancellationToken ct)
        => Ok(await _mcpService.GetAllServersAsync(new GetAllServersRequest
        {
            Source = source,
            Category = category,
            Search = search,
            EnabledState = enabledState,
            InstalledState = installedState
        }, ct));

    /// <summary>
    /// 获取单个 MCP 服务定义。
    /// Gets a single MCP server definition.
    /// </summary>
    [HttpGet("servers/{id:guid}")]
    public async Task<ActionResult<McpServerDto>> GetServerById(Guid id, CancellationToken ct)
    {
        var result = await _mcpService.GetServerByIdAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 创建 MCP 服务定义。
    /// Creates an MCP server definition.
    /// </summary>
    [HttpPost("servers")]
    public async Task<ActionResult<McpServerDto>> CreateServer([FromBody] CreateMcpServerInput input, CancellationToken ct)
        => Ok(await _mcpService.CreateCustomServerAsync(input, ct));

    /// <summary>
    /// 检查 MCP 服务删除/卸载是否会被阻塞。
    /// Checks whether deleting or uninstalling the MCP server would be blocked.
    /// </summary>
    [HttpGet("servers/{id:guid}/uninstall-check")]
    public async Task<ActionResult<McpUninstallCheckResultDto>> CheckUninstall(Guid id, CancellationToken ct)
        => Ok(await _mcpService.CheckUninstallAsync(id, ct));

    /// <summary>
    /// 更新 MCP 服务定义。
    /// Updates an MCP server definition.
    /// </summary>
    [HttpPut("servers/{id:guid}")]
    public async Task<ActionResult<McpServerDto>> UpdateServer(Guid id, [FromBody] UpdateMcpServerInput input, CancellationToken ct)
    {
        var result = await _mcpService.UpdateServerAsync(id, input, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 删除 MCP 服务定义。
    /// Deletes an MCP server definition.
    /// </summary>
    [HttpDelete("servers/{id:guid}")]
    public async Task<ActionResult<DeleteMcpServerResultDto>> DeleteServer(Guid id, CancellationToken ct)
        => Ok(await _mcpService.DeleteServerAsync(id, ct));

    /// <summary>
    /// 修复受管安装的 MCP 服务。
    /// Repairs a managed MCP installation.
    /// </summary>
    [HttpPost("servers/{id:guid}/repair")]
    public async Task<ActionResult<McpRepairResultDto>> RepairInstall(Guid id, CancellationToken ct)
        => Ok(await _mcpService.RepairInstallAsync(id, ct));

    /// <summary>
    /// 获取某个服务定义下的配置实例。
    /// Gets configuration instances for a server definition.
    /// </summary>
    [HttpGet("servers/{mcpServerId:guid}/configs")]
    public async Task<ActionResult<List<McpServerConfigDto>>> GetConfigsByServer(Guid mcpServerId, CancellationToken ct)
        => Ok(await _mcpService.GetConfigsByServerAsync(mcpServerId, ct));

    /// <summary>
    /// 获取全部 MCP 配置实例。
    /// Gets all MCP configuration instances.
    /// </summary>
    [HttpGet("configs")]
    public async Task<ActionResult<List<McpServerConfigDto>>> GetAllConfigs(CancellationToken ct)
        => Ok(await _mcpService.GetAllConfigsAsync(ct));

    /// <summary>
    /// 获取单个 MCP 配置实例。
    /// Gets a single MCP configuration instance.
    /// </summary>
    [HttpGet("configs/{id:guid}")]
    public async Task<ActionResult<McpServerConfigDto>> GetConfigById(Guid id, CancellationToken ct)
    {
        var result = await _mcpService.GetConfigByIdAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 创建 MCP 配置实例。
    /// Creates an MCP configuration instance.
    /// </summary>
    [HttpPost("configs")]
    public async Task<ActionResult<McpServerConfigDto>> CreateConfig([FromBody] CreateMcpServerConfigInput input, CancellationToken ct)
        => Ok(await _mcpService.CreateConfigAsync(input, ct));

    /// <summary>
    /// 更新 MCP 配置实例。
    /// Updates an MCP configuration instance.
    /// </summary>
    [HttpPut("configs/{id:guid}")]
    public async Task<ActionResult<McpServerConfigDto>> UpdateConfig(Guid id, [FromBody] UpdateMcpServerConfigInput input, CancellationToken ct)
    {
        var result = await _mcpService.UpdateConfigAsync(id, input, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 删除 MCP 配置实例。
    /// Deletes an MCP configuration instance.
    /// </summary>
    [HttpDelete("configs/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteConfig(Guid id, CancellationToken ct)
        => await _mcpService.DeleteConfigAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>
    /// 测试 MCP 配置连通性。
    /// Tests connectivity for an MCP configuration.
    /// </summary>
    [HttpPost("configs/{id:guid}/test")]
    public async Task<ActionResult<TestMcpConnectionResult>> TestConnection(Guid id, CancellationToken ct)
        => Ok(await _mcpService.TestConnectionAsync(id, ct));

    /// <summary>
    /// 使用草稿配置测试 MCP 连通性。
    /// Tests connectivity using a draft MCP configuration.
    /// </summary>
    [HttpPost("configs/test-draft")]
    public async Task<ActionResult<TestMcpConnectionResult>> TestConnectionDraft([FromBody] TestMcpConnectionDraftInput input, CancellationToken ct)
        => Ok(await _mcpService.TestConnectionDraftAsync(input, ct));

    /// <summary>
    /// 获取角色测试场景的 MCP 绑定列表。
    /// Gets the MCP bindings for an agent-role test chat.
    /// </summary>
    [HttpGet("agent-bindings/{agentRoleId:guid}")]
    public async Task<ActionResult<List<AgentRoleMcpBindingDto>>> GetAgentBindings(Guid agentRoleId, CancellationToken ct)
        => Ok(await _mcpService.GetAgentRoleBindingsAsync(agentRoleId, ct));

    /// <summary>
    /// 替换角色测试场景的 MCP 绑定列表。
    /// Replaces the MCP bindings for an agent-role test chat.
    /// </summary>
    [HttpPut("agent-bindings/{agentRoleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReplaceAgentBindings(
        Guid agentRoleId,
        [FromBody] List<AgentRoleMcpBindingInput> bindings,
        CancellationToken ct)
    {
        await _mcpService.ReplaceAgentRoleBindingsAsync(new ReplaceAgentRoleMcpBindingsRequest
        {
            AgentRoleId = agentRoleId,
            Bindings = bindings
        }, ct);
        return NoContent();
    }

    /// <summary>
    /// 生成新增 MCP 绑定时可直接编辑的默认草稿。
    /// Generates the editable default draft used when adding an MCP binding.
    /// </summary>
    [HttpPost("binding-draft")]
    public async Task<ActionResult<McpBindingDraftDto>> CreateBindingDraft([FromBody] CreateMcpBindingDraftInput input, CancellationToken ct)
        => Ok(await _mcpService.CreateBindingDraftAsync(input, ct));

    /// <summary>
    /// 获取项目智能体的 MCP 绑定列表。
    /// Gets the MCP bindings for a project agent.
    /// </summary>
    [HttpGet("project-agent-bindings/{projectAgentId:guid}")]
    public async Task<ActionResult<List<ProjectAgentMcpBindingDto>>> GetProjectAgentBindings(Guid projectAgentId, CancellationToken ct)
        => Ok(await _mcpService.GetProjectAgentBindingsAsync(projectAgentId, ct));

    /// <summary>
    /// 替换项目智能体的 MCP 绑定列表。
    /// Replaces the MCP bindings for a project agent.
    /// </summary>
    [HttpPut("project-agent-bindings/{projectAgentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReplaceProjectAgentBindings(
        Guid projectAgentId,
        [FromBody] List<ProjectAgentMcpBindingInput> bindings,
        CancellationToken ct)
    {
        await _mcpService.ReplaceProjectAgentBindingsAsync(new ReplaceProjectAgentMcpBindingsRequest
        {
            ProjectAgentRoleId = projectAgentId,
            Bindings = bindings
        }, ct);
        return NoContent();
    }
}

