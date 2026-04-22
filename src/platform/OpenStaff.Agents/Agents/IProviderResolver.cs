using OpenStaff.Entities;

namespace OpenStaff.Core.Agents;

/// <summary>
/// 供应商解析器 / Provider resolver that expands a provider account into runtime-ready credentials.
/// </summary>
public interface IProviderResolver
{
    /// <summary>解析指定的供应商账户 / Resolve the specified provider account.</summary>
    /// <param name="accountId">供应商账户标识 / Provider account identifier.</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>解析后的供应商信息；找不到时返回 <c>null</c> / Resolved provider information, or <c>null</c> when not found.</returns>
    Task<ResolvedProvider?> ResolveAsync(Guid accountId, CancellationToken ct = default);
}

/// <summary>
/// 解析后的供应商信息 / Provider data ready for runtime agent creation.
/// </summary>
public class ResolvedProvider
{
    /// <summary>解析后的供应商账户实体 / Resolved provider account entity.</summary>
    public ProviderAccount Account { get; set; } = null!;

    /// <summary>可直接使用的 API 密钥 / Ready-to-use API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>API 基地址（从 Env.BaseUrl 解密获得） / Base API URL decrypted from Env.BaseUrl.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// 解密后的 Provider Env JSON。
    /// 运行时可据此重新构造协议对象并解析模型级能力（如实际 ModelProtocolType）。
    /// Decrypted provider environment JSON used to rebuild protocol objects and inspect model-level capabilities.
    /// </summary>
    public string? EnvConfigJson { get; set; }
}
