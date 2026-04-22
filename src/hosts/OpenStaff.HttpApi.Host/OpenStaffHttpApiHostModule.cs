using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Application;
using OpenStaff.Core.Modularity;
using OpenStaff.Core.Notifications;
using OpenStaff.HttpApi;
using OpenStaff.HttpApi.Host.OpenApi;
using OpenStaff.HttpApi.Host.Services;

namespace OpenStaff.HttpApi.Host;

/// <summary>
/// API 层模块，注册 SignalR、OpenAPI 与跨域策略。
/// API-layer module that registers SignalR, OpenAPI, and CORS policies.
/// </summary>
[DependsOn(typeof(OpenStaffApplicationModule), typeof(OpenStaffHttpApiModule))]
public class OpenStaffHttpApiHostModule : OpenStaffModule
{
    /// <summary>
    /// 配置 API 层依赖注入服务。
    /// Configures the dependency injection services required by the API layer.
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Configuration;

        services.AddSignalR();
        services.AddOpenApi(options =>
        {
            options.AddOperationTransformer<JsonEnvelopeOperationTransformer>();
        });
        services.AddSingleton<INotificationService, NotificationService>();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(
                        configuration.GetSection("Cors:Origins").Get<string[]>()
                        ?? ["http://localhost:3000"])
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        return;
    }
}
