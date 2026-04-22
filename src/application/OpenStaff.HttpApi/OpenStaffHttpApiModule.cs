
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Application.Contracts;
using OpenStaff.Core.Modularity;
using OpenStaff.HttpApi.Filters;

namespace OpenStaff.HttpApi;

/// <summary>
/// HTTP API 层模块，负责注册控制器和 JSON 选项。
/// HTTP API-layer module responsible for registering controllers and JSON options.
/// </summary>
[DependsOn(typeof(OpenStaffApplicationContractsModule))]
public class OpenStaffHttpApiModule : OpenStaffModule
{
    /// <summary>
    /// 配置 HTTP API 所需服务。
    /// Configures the services required by the HTTP API layer.
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddControllers(options => options.Filters.Add<JsonResultEnvelopeFilter>())
            .AddApplicationPart(typeof(OpenStaffHttpApiModule).Assembly)
            .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler =
                System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
    }
}
