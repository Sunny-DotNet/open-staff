using Microsoft.AspNetCore.Mvc;
using OpenStaff.Plugins.ModelDataSource;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// 模型数据源管理 / Model data source management
/// </summary>
[ApiController]
[Route("api/models-dev")]
public class ModelsDevController : ControllerBase
{
    private readonly IModelDataSource _dataSource;

    public ModelsDevController(IModelDataSource dataSource) => _dataSource = dataSource;

    /// <summary>
    /// 获取缓存状态 / Get cache status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var vendors = await _dataSource.GetVendorsAsync(ct);
        return Ok(new
        {
            loaded = _dataSource.IsReady,
            lastUpdated = _dataSource.LastUpdatedUtc,
            source = _dataSource.SourceId,
            providerCount = vendors.Count
        });
    }

    /// <summary>
    /// 手动刷新数据 / Manually refresh data
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        await _dataSource.RefreshAsync(ct);
        var vendors = await _dataSource.GetVendorsAsync(ct);
        return Ok(new
        {
            loaded = _dataSource.IsReady,
            lastUpdated = _dataSource.LastUpdatedUtc,
            providerCount = vendors.Count
        });
    }

    /// <summary>
    /// 获取指定供应商的模型列表 / Get models for a provider key
    /// </summary>
    [HttpGet("providers/{providerKey}/models")]
    public async Task<IActionResult> GetModels(string providerKey, CancellationToken ct)
    {
        var models = await _dataSource.GetModelsByVendorAsync(providerKey, ct);
        if (models.Count == 0)
        {
            var vendors = await _dataSource.GetVendorsAsync(ct);
            if (!vendors.Any(v => v.Id.Equals(providerKey, StringComparison.OrdinalIgnoreCase)))
                return NotFound(new { message = $"未找到供应商 '{providerKey}'" });
        }

        return Ok(models.Select(m => new
        {
            m.Id,
            m.Name,
            m.Family,
            reasoning = m.Capabilities.HasFlag(ModelCapability.Reasoning),
            toolCall = m.Capabilities.HasFlag(ModelCapability.FunctionCall),
            attachment = m.Capabilities.HasFlag(ModelCapability.Attachment),
            contextWindow = m.Limits.ContextWindow,
            maxOutput = m.Limits.MaxOutput,
            inputPrice = m.Pricing.Input,
            outputPrice = m.Pricing.Output,
            inputModalities = m.InputModalities.ToString(),
            outputModalities = m.OutputModalities.ToString(),
        }));
    }

    /// <summary>
    /// 获取所有可用供应商列表 / List all available provider keys
    /// </summary>
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders(CancellationToken ct)
    {
        var vendors = await _dataSource.GetVendorsAsync(ct);
        var tasks = vendors.Select(async v =>
        {
            var models = await _dataSource.GetModelsByVendorAsync(v.Id, ct);
            return new
            {
                key = v.Id,
                name = v.Name,
                modelCount = models.Count
            };
        });
        return Ok(await Task.WhenAll(tasks));
    }
}
