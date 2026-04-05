using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Models;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// models.dev 数据管理 / models.dev data management
/// </summary>
[ApiController]
[Route("api/models-dev")]
public class ModelsDevController : ControllerBase
{
    private readonly ModelsDevService _modelsDev;

    public ModelsDevController(ModelsDevService modelsDev) => _modelsDev = modelsDev;

    /// <summary>
    /// 获取缓存状态 / Get cache status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            loaded = _modelsDev.IsLoaded,
            lastUpdated = _modelsDev.LastUpdated,
            providerCount = _modelsDev.GetProviderKeys().Count
        });
    }

    /// <summary>
    /// 手动刷新 models.dev 数据 / Manually refresh models.dev data
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        await _modelsDev.RefreshAsync(ct);
        return Ok(new
        {
            loaded = _modelsDev.IsLoaded,
            lastUpdated = _modelsDev.LastUpdated,
            providerCount = _modelsDev.GetProviderKeys().Count
        });
    }

    /// <summary>
    /// 获取指定供应商的模型列表 / Get models for a provider key
    /// </summary>
    [HttpGet("providers/{providerKey}/models")]
    public IActionResult GetModels(string providerKey)
    {
        var models = _modelsDev.GetModels(providerKey);
        if (models.Count == 0)
        {
            var provider = _modelsDev.GetProvider(providerKey);
            if (provider == null) return NotFound(new { message = $"未找到供应商 '{providerKey}'" });
        }

        return Ok(models.Select(m => new
        {
            m.Id,
            m.Name,
            m.Family,
            m.Reasoning,
            m.ToolCall,
            m.Attachment,
            contextWindow = m.ContextWindow,
            maxOutput = m.MaxOutput,
            m.InputPrice,
            m.OutputPrice,
            inputModalities = m.InputModalities,
            outputModalities = m.OutputModalities,
        }));
    }

    /// <summary>
    /// 获取所有可用供应商列表 / List all available provider keys
    /// </summary>
    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        var keys = _modelsDev.GetProviderKeys();
        var result = keys.Select(k =>
        {
            var p = _modelsDev.GetProvider(k);
            return new
            {
                key = k,
                name = p?.Name,
                modelCount = p?.Models.Count ?? 0
            };
        });
        return Ok(result);
    }
}
