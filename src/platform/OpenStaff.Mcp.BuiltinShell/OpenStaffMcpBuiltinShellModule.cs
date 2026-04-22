using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Mcp;
using OpenStaff.Mcp.Builtin;

namespace OpenStaff.Mcp.BuiltinShell;

[DependsOn(typeof(OpenStaffMcpModule))]
public sealed class OpenStaffMcpBuiltinShellModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IBuiltinMcpToolProvider, ShellBuiltinMcpToolProvider>();
        context.Services.AddHostedService<BuiltinShellMcpServerSeedService>();
    }
}
