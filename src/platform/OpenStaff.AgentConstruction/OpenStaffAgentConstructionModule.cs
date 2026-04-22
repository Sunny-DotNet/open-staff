using OpenStaff.Core.Modularity;
using OpenStaff.Mcp;

namespace OpenStaff;

/// <summary>
/// Agent construction module. This project will own prompt assembly and capability planning.
/// </summary>
[DependsOn(
    typeof(OpenStaffAgentsModule),
    typeof(OpenStaffMcpModule),
    typeof(OpenStaffSkillsModule))]
public class OpenStaffAgentConstructionModule : OpenStaffModule
{
}
