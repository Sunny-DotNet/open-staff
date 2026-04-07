using OpenStaff.Application.Contracts.AgentRoles.Dtos;

namespace OpenStaff.Application.Contracts.AgentRoles;

public interface IAgentRoleAppService
{
    Task<List<AgentRoleDto>> GetAllAsync(CancellationToken ct = default);
    Task<AgentRoleDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AgentRoleDto> CreateAsync(CreateAgentRoleInput input, CancellationToken ct = default);
    Task<AgentRoleDto?> UpdateAsync(Guid id, UpdateAgentRoleInput input, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> ResetVendorAsync(string providerType, CancellationToken ct = default);
    Task<Guid> TestChatAsync(TestChatRequest request, CancellationToken ct = default);
    List<ProviderSchemaDto> GetProviderSchemas();
}
