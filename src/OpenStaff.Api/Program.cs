using Microsoft.EntityFrameworkCore;
using OpenStaff.Api.Hubs;
using OpenStaff.Api.Middleware;
using OpenStaff.Api.Services;
using OpenStaff.Agents;
using OpenStaff.Agents.Orchestrator;
using OpenStaff.Agents.Prompts;
using OpenStaff.Agents.Roles;
using OpenStaff.Agents.Tools;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Orchestration;
using OpenStaff.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Aspire 服务默认值 / Aspire service defaults
builder.AddServiceDefaults();

// 数据库与基础设施 / Database and infrastructure
// 默认使用 ~/.staff/openstaff.db / Default: ~/.staff/openstaff.db
var staffDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".staff");
Directory.CreateDirectory(staffDir);
var defaultDbPath = Path.Combine(staffDir, "openstaff.db");

var connectionString = builder.Configuration.GetConnectionString("openstaff")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Data Source={defaultDbPath}";
var encryptionKey = builder.Configuration["Security:EncryptionKey"];
builder.Services.AddInfrastructure(connectionString, encryptionKey);

// 智能体工具与提示词 / Agent tools and prompts
builder.Services.AddSingleton<IAgentToolRegistry, AgentToolRegistry>();
builder.Services.AddSingleton<IPromptLoader, EmbeddedPromptLoader>();

// 智能体工厂 — 从嵌入式角色配置创建 StandardAgent / Agent factory — creates StandardAgent from embedded role configs
builder.Services.AddSingleton<AgentFactory>(sp =>
{
    var toolRegistry = sp.GetRequiredService<IAgentToolRegistry>();
    var promptLoader = sp.GetRequiredService<IPromptLoader>();
    var factory = new AgentFactory(sp, toolRegistry, promptLoader);

    // 加载所有嵌入式角色配置 / Load all embedded role configurations
    foreach (var config in RoleConfigLoader.LoadAll())
    {
        factory.RegisterRole(config);
    }

    return factory;
});
builder.Services.AddSingleton<OrchestrationService>();
builder.Services.AddSingleton<IOrchestrator>(sp => sp.GetRequiredService<OrchestrationService>());

// 控制器 / Controllers
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

// SignalR 实时通信 / SignalR real-time communication
builder.Services.AddSignalR();

// CORS 跨域 / CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:3000" })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// 应用服务 / Application services
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<AgentService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddSingleton<FileProviderService>();
builder.Services.AddHttpClient<GitHubDeviceAuthService>();
builder.Services.AddHttpClient<ModelListingService>();

// 数据库种子 / Database seed
builder.Services.AddHostedService<OpenStaff.Api.Services.RoleSeedService>();

// 事件转发到 SignalR / Forward events to SignalR
builder.Services.AddHostedService<SignalREventForwarder>();

var app = builder.Build();

// 自动迁移数据库 / Auto-migrate database on startup
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OpenStaff.Infrastructure.Persistence.AppDbContext>();
    await db.Database.MigrateAsync();
}

// 种子默认模型供应商（文件系统）/ Seed default model providers (file-based)
app.Services.GetRequiredService<FileProviderService>().SeedDefaults();

// 中间件 / Middleware
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<LocaleMiddleware>();

app.UseCors();
app.MapControllers();

// SignalR Hubs
app.MapHub<AgentHub>("/hubs/agent");
app.MapHub<ProjectHub>("/hubs/project");

// Aspire 健康检查端点 / Aspire health check endpoints
app.MapDefaultEndpoints();

app.Run();
