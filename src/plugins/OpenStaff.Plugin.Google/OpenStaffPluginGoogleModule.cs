using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Platform;
using OpenStaff.Provider;
using OpenStaff.Provider.Platforms;

namespace OpenStaff.Plugin.Google;

/// <summary>
/// Startup plugin module for the Google vendor. Referencing this assembly makes the
/// Google platform capabilities and vendor services available without hardcoded application registrations.
/// </summary>
[StartupPluginModule]
[DependsOn(typeof(ProviderAbstractionsModule))]
public sealed class OpenStaffPluginGoogleModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddSingleton<GooglePlatformMetadataService>();
        services.AddSingleton<GoogleModelCatalogService>();
        services.AddSingleton<GoogleConfigurationService>();
        services.AddSingleton<GoogleTaskAgentFactory>();
        services.AddSingleton<GooglePlatform>();
        services.AddSingleton<IPlatform>(sp => sp.GetRequiredService<GooglePlatform>());
    }
}
