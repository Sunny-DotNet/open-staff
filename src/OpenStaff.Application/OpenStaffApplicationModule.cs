using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent;
using OpenStaff.Agent.Builtin;
using OpenStaff.Application.AgentRoles;
using OpenStaff.Application.Agents;
using OpenStaff.Application.Auth;
using OpenStaff.Application.Contracts.AgentRoles;
using OpenStaff.Application.Contracts.Agents;
using OpenStaff.Application.Contracts.Auth;
using OpenStaff.Application.Contracts.Files;
using OpenStaff.Application.Contracts.McpServers;
using OpenStaff.Application.Contracts.ModelData;
using OpenStaff.Application.Contracts.Monitor;
using OpenStaff.Application.Contracts.Projects;
using OpenStaff.Application.Contracts.Providers;
using OpenStaff.Application.Contracts.Sessions;
using OpenStaff.Application.Contracts.Settings;
using OpenStaff.Application.Contracts.Tasks;
using OpenStaff.Application.Files;
using OpenStaff.Application.McpServers;
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
using OpenStaff.Marketplace;
using OpenStaff.Marketplace.Internal;
using OpenStaff.Marketplace.Registry;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider;
using static Google.Protobuf.WellKnownTypes.Field.Types;

namespace OpenStaff.Application;

[DependsOn(
    typeof(ProviderAbstractionsModule),
    typeof(OpenStaffProviderOpenAIModule),
    typeof(OpenStaffProviderAnthropicModule),
    typeof(OpenStaffProviderGoogleModule),
    typeof(OpenStaffProviderNewApiModule),
    typeof(OpenStaffProviderGitHubCopilotModule),
    typeof(OpenStaffAgentAbstractionsModule),
    typeof(OpenStaffAgentBuiltinModule),
    typeof(OpenStaffInfrastructureModule), 
    typeof(ModelDataSourceModule),
    typeof(MarketplaceAbstractionsModule),
    typeof(OpenStaffMarketplaceInternalModule),
    typeof(OpenStaffMarketplaceRegistryModule))]
public class OpenStaffApplicationModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // Vendor 智能体供应商（同时注册为 IAgentProvider 和 IVendorAgentProvider）
        services.AddSingleton<OpenStaff.Agent.Vendor.Anthropic.AnthropicAgentProvider>();
        services.AddSingleton<IAgentProvider>(sp => sp.GetRequiredService<OpenStaff.Agent.Vendor.Anthropic.AnthropicAgentProvider>());
        services.AddSingleton<IVendorAgentProvider>(sp => sp.GetRequiredService<OpenStaff.Agent.Vendor.Anthropic.AnthropicAgentProvider>());

        services.AddSingleton<OpenStaff.Agent.Vendor.Google.GoogleAgentProvider>();
        services.AddSingleton<IAgentProvider>(sp => sp.GetRequiredService<OpenStaff.Agent.Vendor.Google.GoogleAgentProvider>());
        services.AddSingleton<IVendorAgentProvider>(sp => sp.GetRequiredService<OpenStaff.Agent.Vendor.Google.GoogleAgentProvider>());

        services.AddSingleton<OpenStaff.Agent.Vendor.GitHubCopilot.GitHubCopilotAgentProvider>();
        services.AddSingleton<IAgentProvider>(sp => sp.GetRequiredService<OpenStaff.Agent.Vendor.GitHubCopilot.GitHubCopilotAgentProvider>());
        services.AddSingleton<IVendorAgentProvider>(sp => sp.GetRequiredService<OpenStaff.Agent.Vendor.GitHubCopilot.GitHubCopilotAgentProvider>());

        services.AddSingleton<OpenStaff.Agent.Vendor.OpenAI.OpenAIAgentProvider>();
        services.AddSingleton<IAgentProvider>(sp => sp.GetRequiredService<OpenStaff.Agent.Vendor.OpenAI.OpenAIAgentProvider>());
        services.AddSingleton<IVendorAgentProvider>(sp => sp.GetRequiredService<OpenStaff.Agent.Vendor.OpenAI.OpenAIAgentProvider>());

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

        // MCP Client 管理
        services.AddSingleton<McpClientManager>();

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
        services.AddHostedService<McpSeedService>();

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
        services.AddScoped<IMcpServerAppService, McpServerAppService>();

        // 市场
        services.AddScoped<Application.Contracts.Marketplace.IMarketplaceAppService, Marketplace.MarketplaceAppService>();
    }


    /// <summary>
    /// 应用初始化 — 启动 GitHub Copilot SDK 测试对话。
    /// CopilotClient 内部会启动捆绑的 Copilot CLI 进程，
    /// 前提：当前机器已通过 `gh auth login` 完成 GitHub 登录。
    /// </summary>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var logger = context.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger<OpenStaffApplicationModule>();

        // 后台启动，不阻塞应用初始化流程
        _ = Task.Run(async () =>
        {
            try
            {
                // ── 1. 启动 Copilot CLI 客户端 ──
                await using var copilotClient = new CopilotClient();
                await copilotClient.StartAsync();
                logger.LogInformation("[CopilotTest] Copilot CLI 客户端已启动");

                // ── 2. 会话配置 ──
                var sessionConfig = new SessionConfig
                {
                    Streaming = true,
                    // 权限回调（必填）：工具执行前由此决定放行或拒绝
                    OnPermissionRequest = (request, _) =>
                    {
                        logger.LogInformation("[CopilotTest] 权限请求: {Kind}", request.Kind);
                        // 测试环境自动批准所有工具调用；生产环境应替换为交互式确认
                        return Task.FromResult(new PermissionRequestResult
                        {
                            Kind = PermissionRequestResultKind.Approved
                        });
                    },
                   
                    // 用户输入回调（可选）：启用 ask_user 工具后 Agent 可向用户提问
                    OnUserInputRequest = (request, _) =>
                    {
                        logger.LogInformation("[CopilotTest] Agent 提问: {Question}", request.Question);
                        // 测试环境自动回复；生产环境应接入前端 UI
                        return Task.FromResult(new UserInputResponse
                        {
                            Answer = "继续",
                            WasFreeform = true
                        });
                    }
                };

                // ── 3. 通过 Agent Framework 包装为 AIAgent ──
                // ownsClient: true → Agent Dispose 时自动关闭 CopilotClient
                var agent = copilotClient.AsAIAgent(sessionConfig, ownsClient: true);

                // ── 4. 发送测试提示 ──
                const string prompt = "List all files in the current directory";
                logger.LogInformation("[CopilotTest] User: {Prompt}", prompt);

                var list = new List<AgentResponseUpdate>();

                try
                {

                    // ── 5. 流式接收响应 ──
                    await foreach (var update in agent.RunStreamingAsync(prompt))
                    {
                        // update.ToString() 输出增量文本片段
                        Console.Write(update);
                        list.Add(update);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                }    
                Console.WriteLine();
                logger.LogInformation("[CopilotTest] 测试对话完成");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[CopilotTest] Copilot 测试失败");
            }
        });
    }

}
