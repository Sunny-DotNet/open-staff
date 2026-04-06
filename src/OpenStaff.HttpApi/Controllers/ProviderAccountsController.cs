using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Contracts.Auth;
using OpenStaff.Application.Contracts.Providers;
using OpenStaff.Application.Contracts.Providers.Dtos;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/provider-accounts")]
public class ProviderAccountsController : ControllerBase
{
    private readonly IProviderAccountAppService _providerAppService;
    private readonly IDeviceAuthAppService _deviceAuthAppService;

    public ProviderAccountsController(IProviderAccountAppService providerAppService, IDeviceAuthAppService deviceAuthAppService)
    {
        _providerAppService = providerAppService;
        _deviceAuthAppService = deviceAuthAppService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _providerAppService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _providerAppService.GetByIdAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProviderAccountInput input, CancellationToken ct)
        => Ok(await _providerAppService.CreateAsync(input, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProviderAccountInput input, CancellationToken ct)
    {
        var result = await _providerAppService.UpdateAsync(id, input, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _providerAppService.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("{id:guid}/models")]
    public async Task<IActionResult> ListModels(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _providerAppService.ListModelsAsync(id, ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (HttpRequestException ex) { return StatusCode(502, new { message = $"无法连接到供应商 API: {ex.Message}" }); }
    }

    // Device auth endpoints
    [HttpPost("{id:guid}/device-auth")]
    public async Task<IActionResult> InitiateDeviceAuth(Guid id, CancellationToken ct)
    {
        try { return Ok(await _deviceAuthAppService.InitiateAsync(id, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (HttpRequestException ex) { return StatusCode(502, new { message = $"无法连接到 GitHub: {ex.Message}" }); }
    }

    [HttpPost("{id:guid}/device-auth/poll")]
    public async Task<IActionResult> PollDeviceAuth(Guid id, CancellationToken ct)
        => Ok(await _deviceAuthAppService.PollAsync(id, ct));

    [HttpDelete("{id:guid}/device-auth")]
    public IActionResult CancelDeviceAuth(Guid id)
    {
        _deviceAuthAppService.Cancel(id);
        return NoContent();
    }
}
