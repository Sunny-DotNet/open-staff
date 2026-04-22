using OpenStaff.Core.Modularity;
using OpenStaff.Mcp;

namespace OpenStaff.Infrastructure;

/// <summary>
/// 聚合已经拆分出的基础设施子域模块，保留过渡期兼容入口。
/// Aggregates the extracted infrastructure subdomain modules and keeps a compatibility entry point during the transition.
/// </summary>
[DependsOn(
    typeof(OpenStaffCoreModule),
    typeof(OpenStaffAgentConstructionModule),
    typeof(OpenStaffProvidersModule),
    typeof(OpenStaffEntityFrameworkCoreModule),
    typeof(OpenStaffMcpModule),
    typeof(OpenStaffSkillsModule),
    typeof(OpenStaffPluginsModule),
    typeof(OpenStaffWorkspaceModule),
    typeof(OpenStaffSecurityModule),
    typeof(OpenStaffEventsModule))]
public class OpenStaffInfrastructureModule : OpenStaffModule
{
}
