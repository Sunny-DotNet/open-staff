using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// 系统设置应用服务契约。
/// Application service contract for reading and updating system settings.
/// </summary>
public interface ISettingsApiService : IApiServiceBase
{
    /// <summary>
    /// 获取所有原始键值设置。
    /// Gets all raw key-value settings.
    /// </summary>
    Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// 批量更新原始键值设置。
    /// Updates raw key-value settings in a batch.
    /// </summary>
    Task UpdateAsync(Dictionary<string, string> settings, CancellationToken ct = default);

    /// <summary>
    /// 获取带默认值的类型化系统设置。
    /// Gets the typed system settings with default values applied.
    /// </summary>
    Task<SystemSettingsDto> GetSystemAsync(CancellationToken ct = default);

    /// <summary>
    /// 保存类型化系统设置。
    /// Persists the typed system settings.
    /// </summary>
    Task UpdateSystemAsync(SystemSettingsDto dto, CancellationToken ct = default);
}


