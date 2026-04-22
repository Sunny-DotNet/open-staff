using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Platform;
using OpenStaff.Provider;
using OpenStaff.Provider.Platforms;

namespace OpenStaff.Plugin.OpenAI;

/// <summary>
/// Startup plugin module for the OpenAI vendor. Referencing this assembly makes the
/// OpenAI platform capabilities and vendor services available without hardcoded application registrations.
/// </summary>
[StartupPluginModule]
[DependsOn(typeof(ProviderAbstractionsModule))]
public sealed class OpenStaffPluginOpenAIModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddSingleton<OpenAIPlatformMetadataService>();
        services.AddSingleton<OpenAIModelCatalogService>();
        services.AddSingleton<OpenAITaskAgentFactory>();
        services.AddSingleton<OpenAIPlatform>();
        services.AddSingleton<IPlatform>(sp => sp.GetRequiredService<OpenAIPlatform>());
    }
}
