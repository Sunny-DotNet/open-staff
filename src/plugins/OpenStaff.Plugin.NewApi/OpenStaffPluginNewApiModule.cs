using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Platform;
using OpenStaff.Provider;
using OpenStaff.Provider.Platforms;

namespace OpenStaff.Plugin.NewApi;

/// <summary>
/// Startup plugin module for the NewApi vendor. Referencing this assembly makes the
/// NewApi protocol and vendor metadata available without hardcoded application registrations.
/// </summary>
[StartupPluginModule]
[DependsOn(typeof(ProviderAbstractionsModule))]
public sealed class OpenStaffPluginNewApiModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<NewApiPlatformMetadataService>();
        context.Services.AddSingleton<NewApiPlatform>();
        context.Services.AddSingleton<IPlatform>(sp => sp.GetRequiredService<NewApiPlatform>());
    }
}
