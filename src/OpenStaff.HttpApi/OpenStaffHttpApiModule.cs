using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Application.Contracts;
using OpenStaff.Core.Modularity;

namespace OpenStaff.HttpApi;

[DependsOn(typeof(OpenStaffApplicationContractsModule))]
public class OpenStaffHttpApiModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddControllers()
            .AddApplicationPart(typeof(OpenStaffHttpApiModule).Assembly)
            .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler =
                System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
    }
}
