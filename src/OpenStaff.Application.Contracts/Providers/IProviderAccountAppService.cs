using OpenStaff.Application.Contracts.Providers.Dtos;

namespace OpenStaff.Application.Contracts.Providers;

public interface IProviderAccountAppService
{
    Task<List<ProviderAccountDto>> GetAllAsync(CancellationToken ct = default);
    Task<ProviderAccountDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProviderAccountDto> CreateAsync(CreateProviderAccountInput input, CancellationToken ct = default);
    Task<ProviderAccountDto?> UpdateAsync(Guid id, UpdateProviderAccountInput input, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<List<ProviderModelDto>> ListModelsAsync(Guid id, CancellationToken ct = default);
}
