using OpenStaff.Agent;

namespace OpenStaff.Plugin.NewApi;

public sealed class NewApiPlatformMetadataService : VendorPlatformMetadataBase
{
    public override string ProviderType => "newapi";

    public override string DisplayName => "NewAPI / OneAPI";
}
