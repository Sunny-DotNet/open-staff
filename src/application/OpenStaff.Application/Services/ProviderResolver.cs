using OpenStaff.Core.Agents;

namespace OpenStaff.Application.Providers.Services;
/// <summary>
/// IProviderResolver 实现 — 组合 ProviderAccountService + ApiKeyResolver
/// </summary>
public class ProviderResolver : IProviderResolver
{
    private readonly ProviderAccountService _accountService;
    private readonly ApiKeyResolver _apiKeyResolver;

    /// <summary>
    /// Initializes the scoped provider resolver that composes account lookup with API-key resolution before agent execution.
    /// 初始化用于在代理执行前组合账户查找与 API Key 解析的 Scoped 提供商解析器。
    /// </summary>
    /// <param name="accountService">Provider-account service used to load persisted account settings. / 用于加载持久化账户设置的提供商账户服务。</param>
    /// <param name="apiKeyResolver">Resolver that computes the effective API key and endpoint values. / 用于计算实际 API Key 与端点值的解析器。</param>
    public ProviderResolver(ProviderAccountService accountService, ApiKeyResolver apiKeyResolver)
    {
        _accountService = accountService;
        _apiKeyResolver = apiKeyResolver;
    }

    /// <summary>
    /// Resolves the provider runtime payload by loading the account, resolving its effective API key, and choosing an endpoint override before returning decrypted env/config JSON.
    /// 通过加载账户、解析其实际 API Key，并在返回已解密环境/配置 JSON 之前优先选择端点覆盖值，解析提供商运行时负载。
    /// </summary>
    /// <param name="accountId">Provider account identifier. / 提供商账户标识。</param>
    /// <param name="ct">Cancellation token for asynchronous resolution. / 用于异步解析的取消令牌。</param>
    /// <returns>Resolved provider payload for runtime use, or <see langword="null"/> when the account or key cannot be resolved. / 运行时使用的解析后提供商负载；若账户或密钥无法解析则返回 <see langword="null"/>。</returns>
    public async Task<ResolvedProvider?> ResolveAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await _accountService.GetByIdAsync(accountId);
        if (account == null) return null;

        var resolved = await _apiKeyResolver.ResolveAsync(account, ct);
        if (string.IsNullOrEmpty(resolved.ApiKey)) return null;

        return new ResolvedProvider
        {
            Account = account,
            ApiKey = resolved.ApiKey,
            BaseUrl = resolved.EndpointOverride ?? resolved.BaseUrl,
            EnvConfigJson = await _accountService.DecryptEnvConfigAsync(account, ct)
        };
    }
}

