using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agents;
using OpenStaff.Agent;
using OpenStaff.Application.Contracts;
using OpenStaff.Core.Modularity;

namespace OpenStaff;

/// <summary>
/// Agent runtime module. This project will consolidate runtime orchestration, providers, and session execution.
/// </summary>
[DependsOn(typeof(OpenStaffApplicationContractsModule))]
public class OpenStaffAgentsModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<AgentFactory>();
        context.Services.AddSingleton<IVendorPlatformCatalog, VendorPlatformCatalog>();
    }
}
