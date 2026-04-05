using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agents;
using OpenStaff.Application.Agents;
using OpenStaff.Application.Auth;
using OpenStaff.Application.Models;
using OpenStaff.Application.Orchestration;
using OpenStaff.Application.Projects;
using OpenStaff.Application.Providers;
using OpenStaff.Application.Seeding;
using OpenStaff.Application.Sessions;
using OpenStaff.Application.Settings;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Modularity;
using OpenStaff.Core.Orchestration;
using OpenStaff.Infrastructure;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Application;

[DependsOn(
    typeof(ProviderAbstractionsModule),
    typeof(OpenStaffProviderOpenAIModule),
    typeof(OpenStaffAgentsModule),
    typeof(OpenStaffInfrastructureModule), 
    typeof(ModelDataSourceModule))]
public class OpenStaffApplicationModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 编排服务 — 依赖 AgentFactory + IProviderResolver + INotificationService
        services.AddSingleton<OrchestrationService>(sp =>
        {
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var scopedResolver = new ScopedProviderResolverProxy(scopeFactory);
            return new OrchestrationService(
                sp.GetRequiredService<AgentFactory>(),
                scopedResolver,
                sp.GetRequiredService<Core.Notifications.INotificationService>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OrchestrationService>>());
        });
        services.AddSingleton<IOrchestrator>(sp => sp.GetRequiredService<OrchestrationService>());

        // 会话服务
        services.AddSingleton<SessionStreamManager>();
        services.AddSingleton<SessionRunner>();

        // 应用服务 (Scoped)
        services.AddScoped<ProjectService>();
        services.AddScoped<AgentService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<DbProviderService>();

        // Provider 解析器
        services.AddScoped<ApiKeyResolver>();
        services.AddScoped<ProviderResolver>();
        services.AddScoped<IProviderResolver>(sp => sp.GetRequiredService<ProviderResolver>());

        // 模型列表服务
        services.AddHttpClient<ModelListingService>();

        // 认证服务
        services.AddSingleton<CopilotTokenService>();
        services.AddHttpClient<GitHubDeviceAuthService>();

        // 数据库种子
        services.AddHostedService<RoleSeedService>();
    }


    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        Task.Run(async () => {
            var protocolFactory=context.ServiceProvider.GetRequiredService<IProtocolFactory>();
            var protocols= protocolFactory.AllProtocols();
            protocolFactory.CreateProtocol<NewApiProtocol, NewApiProtocolEnv>(new NewApiProtocolEnv() { BaseUrl="http://localhost:3000",ApiKey= "sk-KmssOEvvBWftuRGUbBgoJFytubLVFXEh7caQGZH5KqrNAqcg" });
            foreach (var protocol in protocols)
            {
                var models =await  protocol.ModelsAsync();
                Console.WriteLine();
            }
        });
    }
}
