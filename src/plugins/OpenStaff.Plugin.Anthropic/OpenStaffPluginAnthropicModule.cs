using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent;
using OpenStaff.Core.Modularity;
using OpenStaff.Platform;
using OpenStaff.Provider;
using OpenStaff.Provider.Platforms;

namespace OpenStaff.Plugin.Anthropic;

/// <summary>
/// Startup plugin module for the Anthropic vendor. Referencing this assembly makes the
/// Anthropic platform capabilities and vendor services available without hardcoded application registrations.
/// </summary>
[StartupPluginModule]
[DependsOn(typeof(ProviderAbstractionsModule))]
public sealed class OpenStaffPluginAnthropicModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddSingleton<AnthropicPlatformMetadataService>();
        services.AddSingleton<AnthropicModelCatalogService>();
        services.AddSingleton<AnthropicConfigurationService>();
        services.AddSingleton<AnthropicTaskAgentFactory>();
        services.AddSingleton<AnthropicPlatform>();
        services.AddSingleton<IAgentProvider>(sp => sp.GetRequiredService<AnthropicPlatform>());
        services.AddSingleton<IPlatform>(sp => sp.GetRequiredService<AnthropicPlatform>());
    }
}
