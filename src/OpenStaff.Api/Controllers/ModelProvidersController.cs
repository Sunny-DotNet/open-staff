using Microsoft.AspNetCore.Mvc;
using OpenStaff.Api.Services;
using OpenStaff.Core.Models;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// 模型供应商控制器 / Model providers controller
/// </summary>
[ApiController]
[Route("api/model-providers")]
public class ModelProvidersController : ControllerBase
{
    private readonly FileProviderService _providerService;
    private readonly GitHubDeviceAuthService _deviceAuthService;
    private readonly ModelListingService _modelListingService;

    public ModelProvidersController(FileProviderService providerService, GitHubDeviceAuthService deviceAuthService, ModelListingService modelListingService)
    {
        _providerService = providerService;
        _deviceAuthService = deviceAuthService;
        _modelListingService = modelListingService;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var providers = _providerService.GetAll();
        var result = providers.Select(p => new
        {
            p.Id,
            p.Name,
            providerType = p.ProviderType,
            p.BaseUrl,
            apiKeyMode = p.ApiKeyMode,
            apiKeyEnvVar = p.ApiKeyEnvVar,
            hasApiKey = !string.IsNullOrEmpty(p.ApiKeyEncrypted),
            p.DefaultModel,
            isEnabled = p.IsEnabled,
            isBuiltin = p.IsBuiltin,
            p.CreatedAt,
            p.UpdatedAt
        });
        return Ok(result);
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateProviderRequest request)
    {
        var provider = _providerService.Create(request);
        return Ok(provider);
    }

    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id, [FromBody] UpdateProviderRequest request)
    {
        var provider = _providerService.Update(id, request);
        if (provider == null) return NotFound();
        return Ok(new
        {
            provider.Id,
            provider.Name,
            providerType = provider.ProviderType,
            provider.BaseUrl,
            apiKeyMode = provider.ApiKeyMode,
            apiKeyEnvVar = provider.ApiKeyEnvVar,
            hasApiKey = !string.IsNullOrEmpty(provider.ApiKeyEncrypted),
            provider.DefaultModel,
            isEnabled = provider.IsEnabled,
            isBuiltin = provider.IsBuiltin,
            provider.CreatedAt,
            provider.UpdatedAt
        });
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        var result = _providerService.Delete(id);
        if (!result) return NotFound();
        return NoContent();
    }

    // ===== 模型列表 =====

    /// <summary>
    /// 获取供应商可用模型列表 / List available models from a provider
    /// </summary>
    [HttpGet("{id:guid}/models")]
    public async Task<IActionResult> ListModels(Guid id, CancellationToken cancellationToken)
    {
        var provider = _providerService.GetById(id);
        if (provider == null) return NotFound();

        if (!provider.IsEnabled)
        {
            return BadRequest(new { message = "该供应商尚未启用" });
        }

        try
        {
            var models = await _modelListingService.ListModelsAsync(provider, cancellationToken);
            return Ok(models.Select(m => new { m.Id, m.DisplayName }));
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

    /// <summary>
    /// 发起 GitHub 设备码授权
    /// </summary>
    [HttpPost("{id:guid}/device-auth")]
    public async Task<IActionResult> InitiateDeviceAuth(Guid id, CancellationToken cancellationToken)
    {
        var provider = _providerService.GetById(id);
        if (provider == null) return NotFound();

        if (provider.ProviderType != ProviderTypes.GitHubCopilot)
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

    /// <summary>
    /// 轮询 GitHub 设备码授权状态
    /// </summary>
    [HttpPost("{id:guid}/device-auth/poll")]
    public async Task<IActionResult> PollDeviceAuth(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deviceAuthService.PollAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 取消设备码授权
    /// </summary>
    [HttpDelete("{id:guid}/device-auth")]
    public IActionResult CancelDeviceAuth(Guid id)
    {
        _deviceAuthService.Cancel(id);
        return NoContent();
    }
}
