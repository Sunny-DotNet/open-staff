using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Api.Services;
using OpenStaff.Application;
using OpenStaff.Core.Modularity;
using OpenStaff.Core.Notifications;
using OpenStaff.HttpApi;

namespace OpenStaff.Api;

[DependsOn(typeof(OpenStaffApplicationModule), typeof(OpenStaffHttpApiModule))]
public class OpenStaffApiModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Configuration;

        // SignalR
        services.AddSignalR();

        // OpenAPI (Scalar)
        services.AddOpenApi();

        // 通知服务（依赖 SignalR IHubContext，必须在 Api 层注册）
        services.AddSingleton<INotificationService, NotificationService>();

        // CORS
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
}
