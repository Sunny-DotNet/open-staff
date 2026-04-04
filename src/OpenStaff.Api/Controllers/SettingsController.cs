using Microsoft.AspNetCore.Mvc;
using OpenStaff.Api.Services;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// 全局设置控制器 / Global settings controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _settingsService;

    public SettingsController(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>获取所有设置 / Get all settings</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetAllSettingsAsync(cancellationToken);
        return Ok(settings);
    }

    /// <summary>更新设置 / Update settings</summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] Dictionary<string, string> settings, CancellationToken cancellationToken)
    {
        await _settingsService.UpdateSettingsAsync(settings, cancellationToken);
        return Ok(new { message = "设置已更新 / Settings updated" });
    }
}
