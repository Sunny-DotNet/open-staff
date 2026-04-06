using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agents;
using OpenStaff.Application.Agents;
using OpenStaff.Application.AgentRoles;
using OpenStaff.Application.Auth;
using OpenStaff.Application.Contracts.Agents;
using OpenStaff.Application.Contracts.AgentRoles;
using OpenStaff.Application.Contracts.Auth;
using OpenStaff.Application.Contracts.Files;
using OpenStaff.Application.Contracts.ModelData;
using OpenStaff.Application.Contracts.Monitor;
using OpenStaff.Application.Contracts.Projects;
using OpenStaff.Application.Contracts.Providers;
using OpenStaff.Application.Contracts.Sessions;
using OpenStaff.Application.Contracts.Settings;
using OpenStaff.Application.Contracts.Tasks;
using OpenStaff.Application.Files;
using OpenStaff.Application.ModelData;
using OpenStaff.Application.Monitor;
using OpenStaff.Application.Orchestration;
using OpenStaff.Application.Projects;
using OpenStaff.Application.Providers;
using OpenStaff.Application.Seeding;
using OpenStaff.Application.Sessions;
using OpenStaff.Application.Settings;
using OpenStaff.Application.Tasks;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Modularity;
using OpenStaff.Core.Orchestration;
using OpenStaff.Infrastructure;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider;

namespace OpenStaff.Application;

[DependsOn(
    typeof(ProviderAbstractionsModule),
    typeof(OpenStaffProviderOpenAIModule),
    typeof(OpenStaffProviderAnthropicModule),
    typeof(OpenStaffProviderGoogleModule),
    typeof(OpenStaffProviderNewApiModule),
    typeof(OpenStaffProviderGitHubCopilotModule),
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
        services.AddScoped<ProviderAccountService>();

        // Provider 解析器
        services.AddScoped<ApiKeyResolver>();
        services.AddScoped<ProviderResolver>();
        services.AddScoped<IProviderResolver>(sp => sp.GetRequiredService<ProviderResolver>());

        // 认证服务
        services.AddSingleton<CopilotTokenService>();
        services.AddHttpClient<GitHubDeviceAuthService>();

        // 数据库种子
        services.AddHostedService<RoleSeedService>();

        // Application Services (Contracts implementations)
        services.AddScoped<IProjectAppService, ProjectAppService>();
        services.AddScoped<IProviderAccountAppService, ProviderAccountAppService>();
        services.AddScoped<IDeviceAuthAppService, DeviceAuthAppService>();
        services.AddScoped<IAgentRoleAppService, AgentRoleAppService>();
        services.AddScoped<ISessionAppService, SessionAppService>();
        services.AddScoped<ITaskAppService, TaskAppService>();
        services.AddScoped<IFileAppService, FileAppService>();
        services.AddScoped<IMonitorAppService, MonitorAppService>();
        services.AddScoped<ISettingsAppService, SettingsAppService>();
        services.AddScoped<IAgentAppService, AgentAppService>();
        services.AddScoped<IModelDataAppService, ModelDataAppService>();
    }
}
