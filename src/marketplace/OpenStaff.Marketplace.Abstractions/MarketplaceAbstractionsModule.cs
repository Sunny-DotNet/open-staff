using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenStaff.Core.Modularity;
using OpenStaff.Marketplace.Options;

namespace OpenStaff.Marketplace;

/// <summary>
/// Marketplace 抽象模块，注册市场源选项与源工厂。
/// Marketplace abstraction module that registers marketplace source options and the source factory.
/// </summary>
public class MarketplaceAbstractionsModule : OpenStaffModule
{
    /// <summary>
    /// 配置市场抽象层所需服务。
    /// Configures the services required by the marketplace abstraction layer.
    /// </summary>
    /// <param name="context">
    /// 模块服务配置上下文。
    /// Module service configuration context.
    /// </param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<MarketplaceOptions>(options => { });
        context.Services.AddSingleton<IMarketplaceSourceFactory, MarketplaceSourceFactory>();
    }
}

/// <summary>
/// 市场源工厂，用于实例化与检索已注册的市场源。
/// Marketplace source factory used to instantiate and retrieve registered marketplace sources.
/// </summary>
public interface IMarketplaceSourceFactory
{
    /// <summary>
    /// 获取所有已注册的市场源实例。
    /// Gets instances for all registered marketplace sources.
    /// </summary>
    /// <returns>
    /// 市场源实例列表。
    /// List of marketplace source instances.
    /// </returns>
    IReadOnlyList<IMcpMarketplaceSource> GetAllSources();

    /// <summary>
    /// 按源键获取单个市场源。
    /// Gets a single marketplace source by its source key.
    /// </summary>
    /// <param name="sourceKey">
    /// 市场源键。
    /// Marketplace source key.
    /// </param>
    /// <returns>
    /// 匹配的市场源；未找到时返回 <see langword="null" />。
    /// Matching marketplace source, or <see langword="null" /> when not found.
    /// </returns>
    IMcpMarketplaceSource? GetSource(string sourceKey);
}

internal class MarketplaceSourceFactory : IMarketplaceSourceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<MarketplaceOptions> _options;
    private List<IMcpMarketplaceSource>? _cache;

    /// <summary>
    /// 初始化市场源工厂，并保存用于后续延迟实例化市场源的依赖。
    /// Initializes the marketplace source factory and stores the dependencies used for deferred source activation.
    /// </summary>
    /// <param name="serviceProvider">
    /// 用于创建市场源实例的服务提供程序。
    /// Service provider used to create marketplace source instances.
    /// </param>
    /// <param name="options">
    /// 包含已注册市场源类型列表的选项。
    /// Options that hold the registered marketplace source types.
    /// </param>
    public MarketplaceSourceFactory(IServiceProvider serviceProvider, IOptions<MarketplaceOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options;
    }

    /// <summary>
    /// 延迟创建所有已注册市场源，并缓存实例以复用状态型或高成本依赖。
    /// Lazily creates all registered marketplace sources and caches the instances so stateful or expensive dependencies can be reused.
    /// </summary>
    /// <returns>
    /// 按配置注册顺序返回的市场源列表。
    /// Marketplace sources returned in configuration registration order.
    /// </returns>
    public IReadOnlyList<IMcpMarketplaceSource> GetAllSources()
    {
        if (_cache != null) return _cache;

        _cache = [];

        // zh-CN: 延迟实例化所有已注册源，并在首次创建后缓存实例，避免重复构建带状态或昂贵依赖的源对象。
        // en: Lazily instantiate registered sources and cache them after the first creation to avoid rebuilding stateful or dependency-heavy source objects.
        foreach (var sourceType in _options.Value.Sources)
        {
            var source = (IMcpMarketplaceSource)ActivatorUtilities.CreateInstance(_serviceProvider, sourceType);
            _cache.Add(source);
        }

        return _cache;
    }

    /// <summary>
    /// 按源键查找单个市场源，并使用不区分大小写的比较兼容配置值与用户输入。
    /// Finds a single marketplace source by key and uses case-insensitive matching so configuration values and user input resolve consistently.
    /// </summary>
    /// <param name="sourceKey">
    /// 要查找的市场源键。
    /// Marketplace source key to locate.
    /// </param>
    /// <returns>
    /// 匹配的市场源；未注册时返回 <see langword="null" />。
    /// Matching marketplace source, or <see langword="null" /> when no source is registered for the key.
    /// </returns>
    public IMcpMarketplaceSource? GetSource(string sourceKey)
    {
        return GetAllSources().FirstOrDefault(s =>
            string.Equals(s.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase));
    }
}
