namespace OpenStaff.ApiServices;
/// <summary>
/// 模型目录应用服务实现。
/// Application service implementation for model catalog data.
/// </summary>
public class ModelDataApiService : ApiServiceBase, IModelDataApiService
{
    private readonly IModelDataSource _dataSource;

    /// <summary>
    /// Initializes the scoped application service over the shared model-data source so callers can read readiness and refresh state through a stable abstraction.
    /// 使用共享模型数据源初始化 Scoped 应用服务，使调用方能够通过稳定抽象读取就绪状态和刷新状态。
    /// </summary>
    /// <param name="dataSource">Model data source that maintains cached vendor and model catalog data. / 维护供应商与模型目录缓存数据的模型数据源。</param>
    public ModelDataApiService(IModelDataSource dataSource, IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<ModelDataStatusDto> GetStatusAsync(CancellationToken ct)
    {
        var vendors = await _dataSource.GetVendorsAsync(ct);
        return new ModelDataStatusDto
        {
            IsReady = _dataSource.IsReady,
            LastUpdatedUtc = _dataSource.LastUpdatedUtc,
            SourceId = _dataSource.SourceId,
            VendorCount = vendors.Count
        };
    }

    /// <inheritdoc />
    public async Task<ModelDataStatusDto> RefreshAsync(CancellationToken ct)
    {
        await _dataSource.RefreshAsync(ct);
        var vendors = await _dataSource.GetVendorsAsync(ct);
        return new ModelDataStatusDto
        {
            IsReady = _dataSource.IsReady,
            LastUpdatedUtc = _dataSource.LastUpdatedUtc,
            SourceId = _dataSource.SourceId,
            VendorCount = vendors.Count
        };
    }

    /// <inheritdoc />
    public async Task<List<ModelDataDto>> GetModelsAsync(string providerKey, CancellationToken ct)
    {
        var models = await _dataSource.GetModelsByVendorAsync(providerKey, ct);
        return models.Select(m => new ModelDataDto
        {
            Id = m.Id,
            Name = m.Name,
            Description = m.Family,
            Reasoning = m.Capabilities.HasFlag(ModelCapability.Reasoning),
            ToolCall = m.Capabilities.HasFlag(ModelCapability.FunctionCall),
            Attachment = m.Capabilities.HasFlag(ModelCapability.Attachment),
            ContextWindow = m.Limits.ContextWindow,
            MaxOutput = m.Limits.MaxOutput,
            InputPrice = m.Pricing.Input != null ? decimal.TryParse(m.Pricing.Input, out var ip) ? ip : null : null,
            OutputPrice = m.Pricing.Output != null ? decimal.TryParse(m.Pricing.Output, out var op) ? op : null : null,
            InputModalities = m.InputModalities.ToString(),
            OutputModalities = m.OutputModalities.ToString()
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<List<ModelDataProviderDto>> GetProvidersAsync(CancellationToken ct)
    {
        var vendors = await _dataSource.GetVendorsAsync(ct);
        var result = new List<ModelDataProviderDto>();

        foreach (var v in vendors)
        {
            var models = await _dataSource.GetModelsByVendorAsync(v.Id, ct);
            result.Add(new ModelDataProviderDto
            {
                Key = v.Id,
                Name = v.Name,
                ModelCount = models.Count
            });
        }

        return result;
    }
}




