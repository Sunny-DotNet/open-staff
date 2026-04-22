using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent;
using OpenStaff.AgentSouls;
using OpenStaff.Agent.Services;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Agent.Builtin;
using OpenStaff.Mcp.Services;
using OpenStaff.Application.AgentRoles.Services;
using OpenStaff.Application.Conversations.Services;
using OpenStaff.Application.Agents.Services;
using OpenStaff.Application.Auth.Services;
using OpenStaff.Application.McpServers.Services;
using OpenStaff.Application.Orchestration.Services;
using OpenStaff.Application.Projects.Services;
using OpenStaff.Application.Providers.Services;
using OpenStaff.Application.Seeding.Services;
using OpenStaff.Application.Services;
using OpenStaff.Application.Sessions.Services;
using OpenStaff.Application.Settings.Services;
using OpenStaff.Application.Skills.Services;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Modularity;
using OpenStaff.Core.Orchestration;
using OpenStaff.Infrastructure;
using OpenStaff.Marketplace;
using OpenStaff.Marketplace.Internal;
using OpenStaff.Marketplace.Registry;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider;
using OpenStaff.ApiServices;
using OpenStaff.Mcp;
using OpenStaff.Mcp.BuiltinShell;
using OpenStaff.TalentMarket;
using OpenStaff.TalentMarket.Services;

namespace OpenStaff.Application;

/// <summary>
/// 应用层模块，负责组装供应商、编排、会话与应用服务实现。
/// Application-layer module that wires together providers, orchestration, session runtime, and app service implementations.
/// </summary>
[DependsOn(
    typeof(ProviderAbstractionsModule),
    typeof(OpenStaffAgentAbstractionsModule),
    typeof(OpenStaffAgentBuiltinModule),
    typeof(OpenStaffAgentSoulsModule),
    typeof(OpenStaffMcpModule),
    typeof(OpenStaffMcpBuiltinShellModule),
    typeof(OpenStaffSkillsModule),
    typeof(OpenStaffTalentMarketModule),
    typeof(OpenStaffInfrastructureModule), 
    typeof(ModelDataSourceModule),
    typeof(MarketplaceAbstractionsModule),
    typeof(OpenStaffMarketplaceInternalModule),
    typeof(OpenStaffMarketplaceRegistryModule))]
public class OpenStaffApplicationModule : OpenStaffModule
{
    /// <summary>
    /// 配置应用层依赖注入服务。
    /// Configures the dependency injection services required by the application layer.
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // zh-CN: 权限请求处理器集中协调 Copilot 等外部代理的授权回调。
        // en: The permission-request handler coordinates authorization callbacks from external agents such as Copilot.
        services.AddOptions<PermissionRequestHandlerOptions>();
        services.AddSingleton<IPermissionRequestHandler, OpenStaff.Agent.Services.PermissionRequestHandler>();

        // zh-CN: 编排服务保留单例生命周期，运行时账号解析已下沉到具体 Provider，因此不再需要作用域 Provider 代理。
        // en: The orchestrator stays singleton-scoped, and provider-account resolution now lives inside concrete runtimes so no scoped provider proxy is needed here.
        services.AddSingleton<OrchestrationService>(sp =>
        {
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return new OrchestrationService(
                sp.GetRequiredService<AgentFactory>(),
                scopeFactory,
                sp.GetRequiredService<IAgentMcpToolService>(),
                sp.GetRequiredService<Core.Notifications.INotificationService>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OrchestrationService>>());
        });
        services.AddSingleton<IOrchestrator>(sp => sp.GetRequiredService<OrchestrationService>());
        services.AddSingleton<IProjectAgentRuntimeCache>(sp => sp.GetRequiredService<OrchestrationService>());
        services.AddSingleton<IAgentMcpToolService, AgentMcpToolService>();
        services.AddSingleton<IAgentSkillRuntimeService, AgentSkillRuntimeService>();
        services.AddSingleton<IAgentSkillScriptRunner, PowerShellAgentSkillScriptRunner>();
        services.AddSingleton<ApplicationAgentRunFactory>();
        services.AddSingleton<IAgentRunFactory>(sp => sp.GetRequiredService<ApplicationAgentRunFactory>());
        services.AddSingleton<IAgentMessageObserver, SessionStreamingAgentMessageObserver>();
        services.AddSingleton<IAgentMessageObserver, ChatMessageProjectionObserver>();
        services.AddSingleton<IAgentMessageObserver, RuntimeMonitoringProjectionObserver>();
        services.AddSingleton<IAgentService, AgentService>();

        // zh-CN: 会话运行时组件集中注册为单例，以便共享流缓冲、取消令牌和项目群组调度状态。
        // en: Register session runtime components as singletons so they can share stream buffers,
        // cancellation tokens, and ProjectGroup dispatch state.
        services.AddSingleton<SessionStreamManager>();
        services.AddSingleton<TaskStreamManager>();
        services.AddSingleton<ProjectGroupExecutionService>();
        services.AddSingleton<ProjectGroupCapabilityService>();
        services.AddSingleton<SessionRunner>();
        services.AddSingleton<McpRuntimeParameterDefaultsService>();
        services.AddSingleton<IMcpConfigurationFileStore, McpConfigurationFileStore>();
        services.AddSingleton<McpResolvedConnectionFactory>();
        services.AddSingleton<McpWarmupCoordinator>();

        // zh-CN: 这些辅助服务依赖请求级 DbContext，因此保持 Scoped 生命周期。
        // en: These helper services depend on request-scoped DbContext instances and therefore remain scoped.
        services.AddScoped<ProjectService>();
        services.AddScoped<ProjectAgentService>();
        services.AddScoped<RoleCapabilityBindingService>();
        services.AddScoped<AgentRoleTemplateImportService>();
        services.AddScoped<ConversationEntryService>();
        services.AddScoped<ConversationTriggerService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<ProviderAccountService>();
        services.AddScoped<ProviderAccountConfigurationService>();

        // zh-CN: Provider 解析链负责把账户配置、API Key 和协议能力拼装成可执行上下文。
        // en: The provider-resolution chain assembles account settings, API keys, and protocol capabilities into an executable context.
        services.AddScoped<ApiKeyResolver>();
        services.AddScoped<ProviderResolver>();
        services.AddScoped<IProviderResolver>(sp => sp.GetRequiredService<ProviderResolver>());

        // zh-CN: 认证相关服务包含长期令牌缓存和 GitHub 设备授权 HTTP 客户端。
        // en: Authentication services include long-lived token caching and the GitHub device-auth HTTP client.
        services.AddSingleton<CopilotTokenService>();
        services.AddHttpClient<GitHubDeviceAuthService>();
        // zh-CN: 种子服务在宿主启动时补齐内置角色、MCP 元数据和默认能力绑定。
        // en: Seed services populate builtin roles, MCP metadata, and default capability bindings during host startup.
        services.AddHostedService<McpHardResetService>();
        services.AddHostedService<ProjectGroupPermissionAutoApprovalService>();
        services.AddHostedService<JobTitleNormalizationSeedService>();
        services.AddHostedService<MonicaRoleSeedService>();
        services.AddHostedService<RoleCapabilitySeedService>();
        services.AddHostedService<McpToolSnapshotPreloadService>();

        // zh-CN: 以下为公开应用契约的实现，统一使用 Scoped 以复用当前请求的基础设施依赖。
        // en: The following types implement public application contracts and stay scoped to reuse request-bound infrastructure dependencies.
        services.AddHttpClient("skills-github", client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenStaff");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        });
        services.AddSingleton<IManagedSkillStore, ManagedSkillStore>();
        services.AddScoped<IProjectApiService, ProjectApiService>();
        services.AddScoped<IProviderAccountApiService, ProviderAccountApiService>();
        services.AddScoped<IDeviceAuthApiService, DeviceAuthApiService>();
        services.AddScoped<IAgentRoleApiService, AgentRoleApiService>();
        services.AddScoped<IAgentSoulApiService, AgentSoulApiService>();
        services.AddScoped<ISessionApiService, SessionApiService>();
        services.AddScoped<ITaskApiService, TaskApiService>();
        services.AddScoped<IFileApiService, FileApiService>();
        services.AddScoped<IMonitorApiService, MonitorApiService>();
        services.AddScoped<ISettingsApiService, SettingsApiService>();
        services.AddScoped<ISkillApiService, SkillApiService>();
        services.AddScoped<IAgentApiService, AgentApiService>();
        services.AddScoped<IModelDataApiService, ModelDataApiService>();
        services.AddSingleton<IMcpReferenceInspector, ApplicationMcpReferenceInspector>();
        services.AddScoped<IMcpApiService, McpServerApiService>();
        services.AddScoped<IMcpServerApiService>(sp => (IMcpServerApiService)sp.GetRequiredService<IMcpApiService>());

        // zh-CN: 市场实现单独放在 marketplace 程序集中，这里仅暴露应用契约。
        // en: Marketplace behavior lives in a dedicated assembly; the application layer only exposes the contract here.
        services.AddScoped<IMarketplaceApiService, MarketplaceApiService>();
        services.AddScoped<ITalentMarketApiService, TalentMarketApiService>();
    }
}



