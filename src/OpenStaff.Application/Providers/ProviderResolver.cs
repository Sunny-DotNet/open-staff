using OpenStaff.Core.Agents;

namespace OpenStaff.Application.Providers;

/// <summary>
/// IProviderResolver 实现 — 组合 ProviderAccountService + ApiKeyResolver
/// </summary>
public class ProviderResolver : IProviderResolver
{
    private readonly ProviderAccountService _accountService;
    private readonly ApiKeyResolver _apiKeyResolver;

    public ProviderResolver(ProviderAccountService accountService, ApiKeyResolver apiKeyResolver)
    {
        _accountService = accountService;
        _apiKeyResolver = apiKeyResolver;
    }

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
            BaseUrl = resolved.EndpointOverride ?? resolved.BaseUrl
        };
    }
}
