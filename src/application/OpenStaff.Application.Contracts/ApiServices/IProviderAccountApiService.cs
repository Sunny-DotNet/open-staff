using OpenStaff.Configurations;
using OpenStaff.Dtos;
using OpenStaff.Entities;
using System.Text.Json;

namespace OpenStaff.ApiServices;
/// <summary>
/// 模型提供商账户应用服务契约。
/// Application service contract for provider account management.
/// </summary>
public interface IProviderAccountApiService:ICrudApiServiceBase<ProviderAccountDto, Guid, ProviderAccountQueryInput, CreateProviderAccountInput, UpdateProviderAccountInput>
{


    /// <summary>
    /// 获取所有提供商。
    /// Gets all configured provider accounts.
    /// </summary>
    Task<List<ProviderInfo>> GetAllProvidersAsync(CancellationToken ct = default);



    /// <summary>
    /// 拉取账户可用模型列表。
    /// Lists the models available to a provider account.
    /// </summary>
    Task<List<ProviderModelDto>> ListModelsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 加载账户的协议配置快照。
    /// Loads the protocol-configuration snapshot for a provider account.
    /// </summary>
    Task<GetConfigurationResult<JsonElement>> LoadConfigurationAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 保存账户的原始协议配置 JSON。
    /// Saves the raw protocol-configuration JSON for a provider account.
    /// </summary>
    Task SaveConfigurationAsync(Guid id, JsonElement configuration, CancellationToken ct = default);
}

