using OpenStaff.Core.Modularity;
using OpenStaff.HttpApi;
using OpenStaff.Infrastructure;
using System.Text.Json;

namespace OpenStaff.ApiHost;

/// <summary>
/// API host startup module. Runtime switchover from OpenStaff.HttpApi.Host will happen in a later phase.
/// </summary>
[DependsOn(
    typeof(OpenStaffInfrastructureModule),
    typeof(OpenStaffHttpApiModule))]
public class OpenStaffApiHostModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    });
    }
}
