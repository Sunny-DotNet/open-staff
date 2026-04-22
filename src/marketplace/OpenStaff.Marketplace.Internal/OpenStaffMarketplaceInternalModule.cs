using OpenStaff.Core.Modularity;
using OpenStaff.Marketplace.Options;

namespace OpenStaff.Marketplace.Internal;

/// <summary>
/// 内置市场模块，注册基于本地数据库的市场源。
/// Internal marketplace module that registers the marketplace source backed by the local database.
/// </summary>
[DependsOn(typeof(MarketplaceAbstractionsModule))]
public class OpenStaffMarketplaceInternalModule : OpenStaffModule
{
    /// <summary>
    /// 将内置市场源加入市场源注册表。
    /// Adds the internal marketplace source to the marketplace source registry.
    /// </summary>
    /// <param name="context">
    /// 模块服务配置上下文。
    /// Module service configuration context.
    /// </param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<MarketplaceOptions>(options =>
        {
            options.AddSource<InternalMcpSource>();
        });
    }
}
