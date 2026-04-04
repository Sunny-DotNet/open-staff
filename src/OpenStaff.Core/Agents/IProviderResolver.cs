using OpenStaff.Core.Models;

namespace OpenStaff.Core.Agents;

/// <summary>
/// 供应商解析器 — 根据供应商 ID 获取 Provider 配置和可用的 API Key
/// Provider resolver — resolves ModelProvider and API key by provider ID
/// </summary>
public interface IProviderResolver
{
    /// <summary>
    /// 解析供应商配置和 API Key
    /// </summary>
    /// <param name="providerId">供应商 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>解析后的供应商信息（Provider + ApiKey）；如果未找到返回 null</returns>
    Task<ResolvedProvider?> ResolveAsync(Guid providerId, CancellationToken ct = default);
}

/// <summary>
/// 解析后的供应商信息
/// </summary>
public class ResolvedProvider
{
    /// <summary>供应商配置</summary>
    public ModelProvider Provider { get; set; } = null!;

    /// <summary>可直接使用的 API Key</summary>
    public string ApiKey { get; set; } = string.Empty;
}
