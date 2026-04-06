using OpenStaff.Core.Modularity;
using OpenStaff.Marketplace.Options;

namespace OpenStaff.Marketplace.Internal;

[DependsOn(typeof(MarketplaceAbstractionsModule))]
public class OpenStaffMarketplaceInternalModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<MarketplaceOptions>(options =>
        {
            options.AddSource<InternalMcpSource>();
        });
    }
}
