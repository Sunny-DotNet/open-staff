namespace OpenStaff.Marketplace.Options;

/// <summary>
/// 市场源注册选项，维护可用市场源类型的列表。
/// Marketplace source registration options that maintain the list of available source types.
/// </summary>
public class MarketplaceOptions
{
    private readonly List<Type> _sources = [];

    /// <summary>
    /// 已注册的市场源类型。
    /// Registered marketplace source types.
    /// </summary>
    public IReadOnlyCollection<Type> Sources => _sources.AsReadOnly();

    /// <summary>
    /// 注册一个市场源类型，重复注册会被忽略。
    /// Registers a marketplace source type and ignores duplicate registrations.
    /// </summary>
    /// <typeparam name="TSource">
    /// 要注册的市场源类型。
    /// Marketplace source type to register.
    /// </typeparam>
    public void AddSource<TSource>() where TSource : IMcpMarketplaceSource
    {
        if (_sources.Contains(typeof(TSource))) return;
        _sources.Add(typeof(TSource));
    }
}
