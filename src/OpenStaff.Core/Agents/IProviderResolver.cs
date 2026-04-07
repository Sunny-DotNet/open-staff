using OpenStaff.Core.Models;

namespace OpenStaff.Core.Agents;

/// <summary>
/// 供应商解析器 — 根据供应商账户 ID 获取配置和可用的 API Key
/// </summary>
public interface IProviderResolver
{
    Task<ResolvedProvider?> ResolveAsync(Guid accountId, CancellationToken ct = default);
}

/// <summary>
/// 解析后的供应商信息
/// </summary>
public class ResolvedProvider
{
    /// <summary>供应商账户</summary>
    public ProviderAccount Account { get; set; } = null!;

    /// <summary>可直接使用的 API Key</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>API 基地址（从 Env.BaseUrl 解密获得）</summary>
    public string? BaseUrl { get; set; }
}
