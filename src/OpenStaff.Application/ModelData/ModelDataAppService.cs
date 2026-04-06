using OpenStaff.Application.Contracts.ModelData;
using OpenStaff.Application.Contracts.ModelData.Dtos;
using OpenStaff.Plugins.ModelDataSource;

namespace OpenStaff.Application.ModelData;

public class ModelDataAppService : IModelDataAppService
{
    private readonly IModelDataSource _dataSource;

    public ModelDataAppService(IModelDataSource dataSource)
    {
        _dataSource = dataSource;
    }

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
