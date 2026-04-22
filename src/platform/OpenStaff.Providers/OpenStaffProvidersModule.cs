using OpenStaff.Core.Modularity;

namespace OpenStaff;

/// <summary>
/// Providers module. This project will consolidate provider protocol adapters and model capability assembly.
/// </summary>
[DependsOn(typeof(OpenStaffAgentConstructionModule))]
public class OpenStaffProvidersModule : OpenStaffModule
{
}
