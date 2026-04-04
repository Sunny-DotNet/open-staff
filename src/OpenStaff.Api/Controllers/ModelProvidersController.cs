using Microsoft.AspNetCore.Mvc;
using OpenStaff.Api.Services;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// 模型供应商控制器 / Model providers controller
/// </summary>
[ApiController]
[Route("api/model-providers")]
public class ModelProvidersController : ControllerBase
{
    private readonly SettingsService _settingsService;

    public ModelProvidersController(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var providers = await _settingsService.GetAllProvidersAsync(cancellationToken);
        return Ok(providers);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProviderRequest request, CancellationToken cancellationToken)
    {
        var provider = await _settingsService.CreateProviderAsync(request, cancellationToken);
        return Ok(provider);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProviderRequest request, CancellationToken cancellationToken)
    {
        var provider = await _settingsService.UpdateProviderAsync(id, request, cancellationToken);
        if (provider == null) return NotFound();
        return Ok(provider);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _settingsService.DeleteProviderAsync(id, cancellationToken);
        if (!result) return NotFound();
        return NoContent();
    }
}
