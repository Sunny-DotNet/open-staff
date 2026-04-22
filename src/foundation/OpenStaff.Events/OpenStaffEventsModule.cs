using OpenStaff.Application.Contracts;
using OpenStaff.Core.Modularity;

namespace OpenStaff;

/// <summary>
/// Events module. This project will own runtime event contracts and event dispatching.
/// </summary>
[DependsOn(typeof(OpenStaffApplicationContractsModule))]
public class OpenStaffEventsModule : OpenStaffModule
{
}
