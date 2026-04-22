using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Infrastructure.Export;
using OpenStaff.Infrastructure.Git;

namespace OpenStaff;

/// <summary>
/// Workspace module. Hosts Git and import/export services around project workspaces.
/// </summary>
[DependsOn(typeof(OpenStaffCoreModule), typeof(OpenStaffEntityFrameworkCoreModule))]
public class OpenStaffWorkspaceModule : OpenStaffModule
{
    /// <summary>
    /// Registers workspace services that operate on repositories and project archives.
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClient();
        context.Services.AddScoped<GitService>();
        context.Services.AddScoped<ProjectExporter>();
        context.Services.AddScoped<ProjectImporter>();
    }
}
