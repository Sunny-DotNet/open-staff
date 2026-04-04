using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;

namespace OpenStaff.Api.Services;

/// <summary>
/// IProviderResolver 实现 — 组合 FileProviderService + ApiKeyResolver
/// </summary>
public class ProviderResolver : IProviderResolver
{
    private readonly FileProviderService _providerService;
    private readonly ApiKeyResolver _apiKeyResolver;

    public ProviderResolver(FileProviderService providerService, ApiKeyResolver apiKeyResolver)
    {
        _providerService = providerService;
        _apiKeyResolver = apiKeyResolver;
    }

    public async Task<ResolvedProvider?> ResolveAsync(Guid providerId, CancellationToken ct = default)
    {
        var provider = _providerService.GetById(providerId);
        if (provider == null) return null;

        var resolved = await _apiKeyResolver.ResolveAsync(provider, ct);
        if (string.IsNullOrEmpty(resolved.ApiKey)) return null;

        // Copilot 可能返回动态 endpoint
        if (!string.IsNullOrEmpty(resolved.EndpointOverride))
        {
            provider.BaseUrl = resolved.EndpointOverride;
        }

        return new ResolvedProvider
        {
            Provider = provider,
            ApiKey = resolved.ApiKey
        };
    }
}
