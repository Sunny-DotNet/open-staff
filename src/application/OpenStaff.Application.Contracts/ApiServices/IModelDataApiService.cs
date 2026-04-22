using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// 模型目录数据应用服务契约。
/// Application service contract for model catalog data.
/// </summary>
public interface IModelDataApiService : IApiServiceBase
{
    /// <summary>
    /// 获取模型数据缓存状态。
    /// Gets the readiness status of the model data cache.
    /// </summary>
    Task<ModelDataStatusDto> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// 触发模型数据刷新。
    /// Triggers a refresh of the model data source.
    /// </summary>
    Task<ModelDataStatusDto> RefreshAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取指定提供商的模型列表。
    /// Gets the models that belong to the specified provider.
    /// </summary>
    Task<List<ModelDataDto>> GetModelsAsync(string providerKey, CancellationToken ct = default);

    /// <summary>
    /// 获取可查询的模型提供商列表。
    /// Gets the list of providers that have model data available.
    /// </summary>
    Task<List<ModelDataProviderDto>> GetProvidersAsync(CancellationToken ct = default);
}


