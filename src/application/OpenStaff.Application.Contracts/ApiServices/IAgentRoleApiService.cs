using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// 智能体角色应用服务契约。
/// Application service contract for agent role management.
/// </summary>
public interface IAgentRoleApiService
    : ICrudApiServiceBase<AgentRoleDto, Guid, AgentRoleQueryInput, CreateAgentRoleInput, UpdateAgentRoleInput>
{
    /// <summary>
    /// 获取所有可用角色。
    /// Gets all available agent roles.
    /// </summary>
    Task<List<AgentRoleDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// 重置指定供应商的虚拟角色物化结果。
    /// Resets the materialized role for a vendor-backed provider type.
    /// </summary>
    Task<bool> ResetVendorAsync(string providerType, CancellationToken ct = default);

    /// <summary>
    /// 以临时会话的方式测试角色回复。
    /// Tests a role response through a temporary chat session.
    /// </summary>
    Task<ConversationTaskOutput> TestChatAsync(TestChatRequest request, CancellationToken ct = default);

    /// <summary>
    /// 获取供应商角色可用模型。
    /// Gets the models exposed by a vendor-backed agent provider.
    /// </summary>
    Task<List<VendorModelDto>> GetVendorModelsAsync(string providerType, CancellationToken ct = default);

    /// <summary>
    /// 预览角色模板导入结果。
    /// Previews how a role-template document would be parsed and resolved locally.
    /// </summary>
    Task<PreviewAgentRoleTemplateImportResultDto> PreviewTemplateImportAsync(PreviewAgentRoleTemplateImportInput input, CancellationToken ct = default);

    /// <summary>
    /// 导入角色模板并同步角色级默认能力绑定。
    /// Imports a role template and synchronizes its role-level default capability bindings.
    /// </summary>
    Task<ImportAgentRoleTemplateResultDto> ImportTemplateAsync(ImportAgentRoleTemplateInput input, CancellationToken ct = default);

    /// <summary>
    /// 获取供应商角色的模型目录状态。
    /// Gets the model-catalog state exposed by a vendor-backed agent provider.
    /// </summary>
    Task<VendorModelCatalogDto?> GetVendorModelCatalogAsync(string providerType, CancellationToken ct = default);

    /// <summary>
    /// 获取供应商 Provider 自身的配置快照。
    /// Gets the provider-level configuration snapshot for a vendor-backed provider.
    /// </summary>
    Task<VendorProviderConfigurationDto?> GetVendorProviderConfigurationAsync(string providerType, CancellationToken ct = default);

    /// <summary>
    /// 更新供应商 Provider 自身的配置。
    /// Updates the provider-level configuration for a vendor-backed provider.
    /// </summary>
    Task<VendorProviderConfigurationDto?> UpdateVendorProviderConfigurationAsync(string providerType, UpdateVendorProviderConfigurationInput input, CancellationToken ct = default);
}



