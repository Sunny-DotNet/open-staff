using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenStaff.Agent;
using OpenStaff.Agent.Vendor.GitHubCopilot;
using OpenStaff.Core.Modularity;
using OpenStaff.Plugin.Services;
using OpenStaff.Provider;
using OpenStaff.Provider.Platforms;

namespace OpenStaff.Plugin.GitHubCopilot;

/// <summary>
/// Startup plugin module for the GitHub Copilot vendor. Referencing this assembly makes the
/// Copilot platform capabilities and vendor services available without hardcoding registrations in the host.
/// </summary>
[StartupPluginModule]
[DependsOn(typeof(ProviderAbstractionsModule))]
public sealed class OpenStaffPluginGitHubCopilotModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddHttpClient();
        services.AddSingleton<CopilotTokenService>();
        services.AddSingleton<IGitHubCopilotTemporarySessionRegistry, GitHubCopilotTemporarySessionRegistry>();
        services.AddSingleton<GitHubCopilotClientHost>();
        services.AddSingleton<IGitHubCopilotClientHost>(sp => sp.GetRequiredService<GitHubCopilotClientHost>());
        services.AddSingleton<IGitHubCopilotSessionManager, GitHubCopilotSessionManager>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<GitHubCopilotClientHost>());

        services.AddSingleton<GitHubCopilotPlatformMetadataService>();
        services.AddSingleton<GitHubCopilotModelCatalogService>();
        services.AddSingleton<GitHubCopilotConfigurationService>();
        services.AddSingleton<GitHubCopilotTaskAgentFactory>();
        services.AddSingleton<GitHubCopilotPlatform>();
        services.AddSingleton<IAgentProvider>(sp => sp.GetRequiredService<GitHubCopilotPlatform>());
        services.AddSingleton<IPlatform>(sp => sp.GetRequiredService<GitHubCopilotPlatform>());
    }
}
