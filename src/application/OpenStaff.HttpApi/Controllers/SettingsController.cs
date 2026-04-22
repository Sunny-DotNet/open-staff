
using Microsoft.AspNetCore.Mvc;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// 系统设置控制器。
/// Controller that exposes system settings endpoints.
/// </summary>
[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsApiService _settingsApiService;

    /// <summary>
    /// 初始化系统设置控制器。
    /// Initializes the system settings controller.
    /// </summary>
    public SettingsController(ISettingsApiService settingsApiService)
    {
        _settingsApiService = settingsApiService;
    }

    /// <summary>
    /// 获取所有原始键值设置。
    /// Gets all raw key-value settings.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<Dictionary<string, string>>> GetAll(CancellationToken ct)
        => Ok(await _settingsApiService.GetAllAsync(ct));

    /// <summary>
    /// 批量更新原始键值设置。
    /// Updates raw key-value settings in a batch.
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<ApiMessageDto>> Update([FromBody] Dictionary<string, string> settings, CancellationToken ct)
    {
        await _settingsApiService.UpdateAsync(settings, ct);
        return Ok(new ApiMessageDto { Message = "ok" });
    }

    /// <summary>
    /// 获取类型化系统设置。
    /// Gets the typed system settings.
    /// </summary>
    [HttpGet("system")]
    public async Task<ActionResult<SystemSettingsDto>> GetSystem(CancellationToken ct)
        => Ok(await _settingsApiService.GetSystemAsync(ct));

    /// <summary>
    /// 保存类型化系统设置。
    /// Saves the typed system settings.
    /// </summary>
    [HttpPut("system")]
    public async Task<ActionResult<ApiMessageDto>> UpdateSystem([FromBody] SystemSettingsDto dto, CancellationToken ct)
    {
        await _settingsApiService.UpdateSystemAsync(dto, ct);
        return Ok(new ApiMessageDto { Message = "ok" });
    }
}

