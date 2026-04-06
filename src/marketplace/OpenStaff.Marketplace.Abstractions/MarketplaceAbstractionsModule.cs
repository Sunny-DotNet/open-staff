using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenStaff.Core.Modularity;
using OpenStaff.Marketplace.Options;

namespace OpenStaff.Marketplace;

public class MarketplaceAbstractionsModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<MarketplaceOptions>(options => { });
        context.Services.AddSingleton<IMarketplaceSourceFactory, MarketplaceSourceFactory>();
    }
}

/// <summary>
/// 市场源工厂 — 根据 MarketplaceOptions 实例化所有注册的源
/// </summary>
public interface IMarketplaceSourceFactory
{
    IReadOnlyList<IMcpMarketplaceSource> GetAllSources();
    IMcpMarketplaceSource? GetSource(string sourceKey);
}

internal class MarketplaceSourceFactory : IMarketplaceSourceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<MarketplaceOptions> _options;
    private List<IMcpMarketplaceSource>? _cache;

    public MarketplaceSourceFactory(IServiceProvider serviceProvider, IOptions<MarketplaceOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options;
    }

    public IReadOnlyList<IMcpMarketplaceSource> GetAllSources()
    {
        if (_cache != null) return _cache;

        _cache = [];
        foreach (var sourceType in _options.Value.Sources)
        {
            var source = (IMcpMarketplaceSource)ActivatorUtilities.CreateInstance(_serviceProvider, sourceType);
            _cache.Add(source);
        }
        return _cache;
    }

    public IMcpMarketplaceSource? GetSource(string sourceKey)
    {
        return GetAllSources().FirstOrDefault(s =>
            string.Equals(s.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase));
    }
}
