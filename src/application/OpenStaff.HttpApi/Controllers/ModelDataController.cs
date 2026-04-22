
using Microsoft.AspNetCore.Mvc;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// 模型目录数据控制器。
/// Controller that exposes model catalog status and refresh endpoints.
/// </summary>
[ApiController]
[Route("api/models-dev")]
public class ModelDataController : ControllerBase
{
    private readonly IModelDataApiService _modelDataApiService;

    /// <summary>
    /// 初始化模型目录数据控制器。
    /// Initializes the model catalog data controller.
    /// </summary>
    /// <param name="modelDataApiService">注入的模型数据应用服务，负责缓存状态查询、刷新以及目录数据读取，控制器仅负责公开 HTTP 接口。 / Injected model-data application service that handles cache status queries, refresh operations, and catalog reads while the controller only exposes HTTP endpoints.</param>
    public ModelDataController(IModelDataApiService modelDataApiService)
    {
        _modelDataApiService = modelDataApiService;
    }

    /// <summary>
    /// 获取模型数据缓存状态。
    /// Gets the current model data cache status.
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ModelDataStatusDto>> GetStatus(CancellationToken ct)
        => Ok(await _modelDataApiService.GetStatusAsync(ct));

    /// <summary>
    /// 触发模型数据刷新。
    /// Triggers a model data refresh.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<ModelDataStatusDto>> Refresh(CancellationToken ct)
        => Ok(await _modelDataApiService.RefreshAsync(ct));

    /// <summary>
    /// 获取指定提供商的模型列表。
    /// Gets the models for a specific provider.
    /// </summary>
    [HttpGet("providers/{providerKey}/models")]
    public async Task<ActionResult<List<ModelDataDto>>> GetModels(string providerKey, CancellationToken ct)
        => Ok(await _modelDataApiService.GetModelsAsync(providerKey, ct));

    /// <summary>
    /// 获取拥有模型目录数据的提供商列表。
    /// Gets the providers that have catalog data available.
    /// </summary>
    [HttpGet("providers")]
    public async Task<ActionResult<List<ModelDataProviderDto>>> GetProviders(CancellationToken ct)
        => Ok(await _modelDataApiService.GetProvidersAsync(ct));
}

