using OpenStaff.Core.Modularity;
using OpenStaff.Provider.Options;

namespace OpenStaff.Provider;

[DependsOn(typeof(ProviderAbstractionsModule))]
public class OpenStaffProviderGoogleModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<ProviderOptions>(options =>
        {
            options.AddProtocol<Protocols.GoogleProtocol>();
        });
    }
}
