using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Providers;
using OpenStaff.Application.Auth;
using OpenStaff.Application.Models;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// 供应商账户控制器 / Provider accounts controller
/// </summary>
[ApiController]
[Route("api/provider-accounts")]
public class ProviderAccountsController : ControllerBase
{
    private readonly ProviderAccountService _accountService;
    private readonly GitHubDeviceAuthService _deviceAuthService;
    private readonly ModelListingService _modelListingService;

    public ProviderAccountsController(ProviderAccountService accountService, GitHubDeviceAuthService deviceAuthService, ModelListingService modelListingService)
    {
        _accountService = accountService;
        _deviceAuthService = deviceAuthService;
        _modelListingService = modelListingService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var accounts = await _accountService.GetAllAsync();
        var result = accounts.Select(a => new
        {
            a.Id,
            a.Name,
            protocolType = a.ProtocolType,
            isEnabled = a.IsEnabled,
            a.CreatedAt,
            a.UpdatedAt
        });
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var account = await _accountService.GetByIdAsync(id);
        if (account == null) return NotFound();

        // Return account with non-secret env config fields
        var envConfig = _accountService.GetEnvConfigDict(account);
        return Ok(new
        {
            account.Id,
            account.Name,
            protocolType = account.ProtocolType,
            isEnabled = account.IsEnabled,
            envConfig,
            account.CreatedAt,
            account.UpdatedAt
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProviderAccountRequest request)
    {
        var account = await _accountService.CreateAsync(request);
        return Ok(new { account.Id, account.Name, protocolType = account.ProtocolType });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProviderAccountRequest request)
    {
        var account = await _accountService.UpdateAsync(id, request);
        if (account == null) return NotFound();
        return Ok(new
        {
            account.Id,
            account.Name,
            protocolType = account.ProtocolType,
            isEnabled = account.IsEnabled,
            account.UpdatedAt
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _accountService.DeleteAsync(id);
        if (!result) return NotFound();
        return NoContent();
    }

    // ===== 模型列表 =====

    [HttpGet("{id:guid}/models")]
    public async Task<IActionResult> ListModels(Guid id, CancellationToken cancellationToken)
    {
        var account = await _accountService.GetByIdAsync(id);
        if (account == null) return NotFound();

        try
        {
            var models = await _modelListingService.ListModelsAsync(account, cancellationToken);
            return Ok(models.Select(m => new
            {
                m.Id,
                m.DisplayName,
                contextWindow = m.ContextWindow,
                maxOutput = m.MaxOutput,
                reasoning = m.Reasoning
            }));
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { message = $"无法连接到供应商 API: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"获取模型列表失败: {ex.Message}" });
        }
    }

    // ===== GitHub 设备码授权 =====

    [HttpPost("{id:guid}/device-auth")]
    public async Task<IActionResult> InitiateDeviceAuth(Guid id, CancellationToken cancellationToken)
    {
        var account = await _accountService.GetByIdAsync(id);
        if (account == null) return NotFound();

        if (account.ProtocolType != "github-copilot")
        {
            return BadRequest(new { message = "仅 GitHub Copilot 支持设备码授权" });
        }

        try
        {
            var result = await _deviceAuthService.InitiateAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { message = $"无法连接到 GitHub: {ex.Message}" });
        }
    }

    [HttpPost("{id:guid}/device-auth/poll")]
    public async Task<IActionResult> PollDeviceAuth(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deviceAuthService.PollAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/device-auth")]
    public IActionResult CancelDeviceAuth(Guid id)
    {
        _deviceAuthService.Cancel(id);
        return NoContent();
    }
}
