using OpenStaff.Application.Contracts.ModelData.Dtos;

namespace OpenStaff.Application.Contracts.ModelData;

public interface IModelDataAppService
{
    Task<ModelDataStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<ModelDataStatusDto> RefreshAsync(CancellationToken ct = default);
    Task<List<ModelDataDto>> GetModelsAsync(string providerKey, CancellationToken ct = default);
    Task<List<ModelDataProviderDto>> GetProvidersAsync(CancellationToken ct = default);
}
