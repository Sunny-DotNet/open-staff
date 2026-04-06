namespace OpenStaff.Marketplace.Options;

/// <summary>
/// 市场源注册选项（类似 ProviderOptions）
/// </summary>
public class MarketplaceOptions
{
    private readonly List<Type> _sources = [];
    public IReadOnlyCollection<Type> Sources => _sources.AsReadOnly();

    public void AddSource<TSource>() where TSource : IMcpMarketplaceSource
    {
        if (_sources.Contains(typeof(TSource))) return;
        _sources.Add(typeof(TSource));
    }
}
