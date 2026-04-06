using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Contracts.Settings;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsAppService _settingsAppService;

    public SettingsController(ISettingsAppService settingsAppService)
    {
        _settingsAppService = settingsAppService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _settingsAppService.GetAllAsync(ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] Dictionary<string, string> settings, CancellationToken ct)
    {
        await _settingsAppService.UpdateAsync(settings, ct);
        return Ok(new { message = "ok" });
    }
}
