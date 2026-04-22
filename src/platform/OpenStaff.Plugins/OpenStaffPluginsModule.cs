using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Application.Contracts;
using OpenStaff.Core.Modularity;
using OpenStaff.Plugins;

namespace OpenStaff;

/// <summary>
/// Plugins module. This project will host plugin discovery, loading, and registration.
/// </summary>
[DependsOn(typeof(OpenStaffApplicationContractsModule))]
public class OpenStaffPluginsModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<PluginLoader>();
    }
}
