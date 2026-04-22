
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// 智能体角色控制器。
/// Controller that exposes agent role management endpoints.
/// </summary>
[ApiController]
[Route("api/agent-roles")]
public class AgentRolesController : ControllerBase
{
    private readonly IAgentRoleApiService _agentRoleApiService;

    /// <summary>
    /// 初始化智能体角色控制器。
    /// Initializes the agent roles controller.
    /// </summary>
    public AgentRolesController(IAgentRoleApiService agentRoleApiService)
    {
        _agentRoleApiService = agentRoleApiService;
    }

    /// <summary>
    /// 获取所有角色。
    /// Gets all agent roles.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AgentRoleDto>>> GetAll(CancellationToken ct)
        => Ok(await _agentRoleApiService.GetAllAsync(ct));

    /// <summary>
    /// 获取单个角色详情。
    /// Gets the details of a single agent role.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AgentRoleDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _agentRoleApiService.GetByIdAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 创建角色。
    /// Creates an agent role.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AgentRoleDto>> Create([FromBody] CreateAgentRoleInput input, CancellationToken ct)
        => Ok(await _agentRoleApiService.CreateAsync(input, ct));

    /// <summary>
    /// 预览角色模板导入结果。
    /// Previews the result of importing a role template.
    /// </summary>
    [HttpPost("preview-import")]
    public async Task<ActionResult<PreviewAgentRoleTemplateImportResultDto>> PreviewImport(
        [FromBody] PreviewAgentRoleTemplateImportInput input,
        CancellationToken ct)
        => Ok(await _agentRoleApiService.PreviewTemplateImportAsync(input, ct));

    /// <summary>
    /// 导入角色模板并同步默认能力绑定。
    /// Imports a role template and synchronizes its default capability bindings.
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<ImportAgentRoleTemplateResultDto>> Import(
        [FromBody] ImportAgentRoleTemplateInput input,
        CancellationToken ct)
        => Ok(await _agentRoleApiService.ImportTemplateAsync(input, ct));

    /// <summary>
    /// 更新角色。
    /// Updates an agent role.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AgentRoleDto>> Update(Guid id, [FromBody] UpdateAgentRoleInput input, CancellationToken ct)
        => Ok(await _agentRoleApiService.UpdateAsync(id, input, ct));

    /// <summary>
    /// 删除角色。
    /// Deletes an agent role.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _agentRoleApiService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// 重置指定供应商的角色物化结果。
    /// Resets the materialized role for the specified vendor provider type.
    /// </summary>
    [HttpPost("vendor/{providerType}/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetVendor(string providerType, CancellationToken ct)
        => await _agentRoleApiService.ResetVendorAsync(providerType, ct) ? NoContent() : NotFound();

    /// <summary>
    /// 获取供应商角色可用模型。
    /// Gets the models exposed by a vendor-backed role provider.
    /// </summary>
    [HttpGet("vendor/{providerType}/models")]
    public async Task<ActionResult<List<VendorModelDto>>> GetVendorModels(string providerType, CancellationToken ct)
        => Ok(await _agentRoleApiService.GetVendorModelsAsync(providerType, ct));

    /// <summary>
    /// 获取供应商角色的模型目录状态。
    /// Gets the model-catalog state exposed by a vendor-backed role provider.
    /// </summary>
    [HttpGet("vendor/{providerType}/model-catalog")]
    public async Task<ActionResult<VendorModelCatalogDto>> GetVendorModelCatalog(string providerType, CancellationToken ct)
    {
        var result = await _agentRoleApiService.GetVendorModelCatalogAsync(providerType, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 获取供应商 Provider 自身的配置快照。
    /// Gets the provider-level configuration snapshot for a vendor-backed provider.
    /// </summary>
    [HttpGet("vendor/{providerType}/configuration")]
    public async Task<ActionResult<VendorProviderConfigurationDto>> GetVendorProviderConfiguration(string providerType, CancellationToken ct)
    {
        var result = await _agentRoleApiService.GetVendorProviderConfigurationAsync(providerType, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 更新供应商 Provider 自身的配置。
    /// Updates the provider-level configuration for a vendor-backed provider.
    /// </summary>
    [HttpPut("vendor/{providerType}/configuration")]
    public async Task<ActionResult<VendorProviderConfigurationDto>> UpdateVendorProviderConfiguration(
        string providerType,
        [FromBody] UpdateVendorProviderConfigurationInput input,
        CancellationToken ct)
    {
        var result = await _agentRoleApiService.UpdateVendorProviderConfigurationAsync(providerType, input, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 启动角色测试聊天。
    /// Starts a test-chat session for a role.
    /// </summary>
    [HttpPost("{id:guid}/test-chat")]
    public async Task<ActionResult<ConversationTaskOutput>> TestChat(Guid id, [FromBody] TestChatBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Message))
            return BadRequest(new ApiMessageDto { Message = "消息不能为空" });

        var request = new TestChatRequest { AgentRoleId = id, Message = body.Message, Override = body.Override };
        var result = await _agentRoleApiService.TestChatAsync(request, ct);
        return Ok(result);
    }

}

/// <summary>
/// 角色测试聊天请求体。
/// Request body for testing role chat.
/// </summary>
public class TestChatBody
{
    /// <summary>测试消息。 / Test message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>临时覆盖配置。 / Temporary override configuration.</summary>
    public AgentRoleInput? Override { get; set; }
}

