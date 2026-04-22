
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenStaff.Configurations;
using OpenStaff.Dtos;
using System.Text.Json;
using OpenStaff.ApiServices;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// 提供商账户控制器。
/// Controller that exposes provider account and device-auth endpoints.
/// </summary>
[ApiController]
[Route("api/provider-accounts")]
public class ProviderAccountsController : ControllerBase
{
    private readonly IProviderAccountApiService _providerApiService;
    private readonly IDeviceAuthApiService _deviceAuthApiService;

    /// <summary>
    /// 初始化提供商账户控制器。
    /// Initializes the provider accounts controller.
    /// </summary>
    /// <param name="providerApiService">注入的提供商账户应用服务，负责账户的查询、创建、更新和模型列表等业务逻辑，控制器仅负责路由和响应封装。 / Injected provider-account application service that handles account queries, creation, updates, and model listing while the controller only manages routing and HTTP responses.</param>
    /// <param name="deviceAuthApiService">注入的设备授权应用服务，负责驱动 GitHub 等外部设备授权流程，供本控制器公开设备授权相关端点。 / Injected device-auth application service that drives external device authorization flows such as GitHub so this controller can expose the related endpoints.</param>
    public ProviderAccountsController(IProviderAccountApiService providerApiService, IDeviceAuthApiService deviceAuthApiService)
    {
        _providerApiService = providerApiService;
        _deviceAuthApiService = deviceAuthApiService;
    }

    /// <summary>
    /// 获取所有提供商账户。
    /// Gets all provider accounts.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProviderAccountDto>>> GetAll([FromQuery] ProviderAccountQueryInput input, CancellationToken ct)
        => Ok(await _providerApiService.GetAllAsync(input, ct));

    /// <summary>
    /// 获取可用提供商元数据列表。
    /// Gets available provider metadata for account creation.
    /// </summary>
    [HttpGet("providers")]
    public async Task<ActionResult<List<ProviderInfo>>> GetProviders(CancellationToken ct)
        => Ok(await _providerApiService.GetAllProvidersAsync(ct));

    /// <summary>
    /// 获取单个提供商账户详情。
    /// Gets the details of a single provider account.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProviderAccountDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _providerApiService.GetByIdAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// 创建提供商账户。
    /// Creates a provider account.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProviderAccountDto>> Create([FromBody] CreateProviderAccountInput input, CancellationToken ct)
        => Ok(await _providerApiService.CreateAsync(input, ct));

    /// <summary>
    /// 更新提供商账户。
    /// Updates a provider account.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProviderAccountDto>> Update(Guid id, [FromBody] UpdateProviderAccountInput input, CancellationToken ct)
        => Ok(await _providerApiService.UpdateAsync(id, input, ct));

    /// <summary>
    /// 获取单个提供商账户的原始配置。
    /// Gets the raw configuration JSON for a single provider account.
    /// </summary>
    [HttpGet("{id:guid}/configuration")]
    public async Task<ActionResult<GetConfigurationResult<JsonElement>>> LoadConfiguration(Guid id, CancellationToken ct)
        => Ok(await _providerApiService.LoadConfigurationAsync(id, ct));

    /// <summary>
    /// 保存单个提供商账户的原始配置。
    /// Saves the raw configuration JSON for a single provider account.
    /// </summary>
    [HttpPut("{id:guid}/configuration")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveConfiguration(Guid id, [FromBody] JsonElement configuration, CancellationToken ct)
    {
        await _providerApiService.SaveConfigurationAsync(id, configuration, ct);
        return NoContent();
    }

    /// <summary>
    /// 删除提供商账户。
    /// Deletes a provider account.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _providerApiService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// 拉取账户可用模型列表。
    /// Lists the models available to the provider account.
    /// </summary>
    [HttpGet("{id:guid}/models")]
    public async Task<ActionResult<List<ProviderModelDto>>> ListModels(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _providerApiService.ListModelsAsync(id, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ApiMessageDto { Message = $"无法连接到供应商 API: {ex.Message}" });
        }
    }

    /// <summary>
    /// 启动设备授权流程。
    /// Starts the device authorization flow.
    /// </summary>
    [HttpPost("{id:guid}/device-auth")]
    public async Task<ActionResult<DeviceCodeDto>> InitiateDeviceAuth(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _deviceAuthApiService.InitiateAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiMessageDto { Message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ApiMessageDto { Message = $"无法连接到 GitHub: {ex.Message}" });
        }
    }

    /// <summary>
    /// 轮询设备授权状态。
    /// Polls the device authorization status.
    /// </summary>
    [HttpPost("{id:guid}/device-auth/poll")]
    public async Task<ActionResult<DeviceAuthPollDto>> PollDeviceAuth(Guid id, CancellationToken ct)
        => Ok(await _deviceAuthApiService.PollAsync(id, ct));

    /// <summary>
    /// 取消设备授权轮询。
    /// Cancels device authorization polling.
    /// </summary>
    [HttpDelete("{id:guid}/device-auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult CancelDeviceAuth(Guid id)
    {
        _deviceAuthApiService.Cancel(id);
        return NoContent();
    }
}

