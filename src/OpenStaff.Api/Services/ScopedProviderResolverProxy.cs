using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;

namespace OpenStaff.Api.Services;

/// <summary>
/// IProviderResolver 的单例代理 — 每次调用时创建新的 DI 作用域
/// 解决单例服务（OrchestrationService）依赖 Scoped 服务（ProviderResolver）的问题
/// </summary>
public class ScopedProviderResolverProxy : IProviderResolver
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedProviderResolverProxy(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<ResolvedProvider?> ResolveAsync(Guid providerId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ProviderResolver>();
        return await resolver.ResolveAsync(providerId, ct);
    }
}
