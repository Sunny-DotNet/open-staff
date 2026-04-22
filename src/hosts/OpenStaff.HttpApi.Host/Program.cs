
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenStaff.HttpApi.Host;
using OpenStaff.HttpApi.Host.Hubs;
using OpenStaff.HttpApi.Host.Middleware;
using OpenStaff.Core.Modularity;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Options;
using OpenStaff.Plugins.ModelDataSource;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// zh-CN: 统一接入 Aspire 默认能力，例如健康检查、遥测和服务发现。
// en: Wire up shared Aspire defaults such as health checks, telemetry, and service discovery.
builder.AddServiceDefaults();

// zh-CN: 按模块注册应用层、HTTP 层和 API 层服务。
// en: Register application, HTTP, and API services through the modular system.
builder.Services.AddOpenStaffModules<OpenStaffHttpApiHostModule>(builder.Configuration);

var app = builder.Build();

// zh-CN: 启动时自动迁移数据库，确保 API 与最新结构保持一致。
// en: Apply database migrations on startup so the API runs against the latest schema.
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var openStaffOptions = scope.ServiceProvider.GetRequiredService<IOptions<OpenStaffOptions>>().Value;
    await ProviderAccountEnvConfigBackfill.BackfillAsync(db, openStaffOptions);
    await db.Database.MigrateAsync();
}

// zh-CN: 在应用启动时执行模块初始化逻辑。
// en: Run module initialization logic during application startup.
app.Services.UseOpenStaffModules();

// zh-CN: 初始化模型目录数据源，确保刷新入口和查询入口可直接工作。
// en: Initialize the model catalog source so refresh and query endpoints work immediately.
await app.Services.GetRequiredService<IModelDataSource>().InitializeAsync();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<LocaleMiddleware>();

app.UseCors();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("OpenStaff API");
        options.WithTheme(ScalarTheme.DeepSpace);
    });
}

// zh-CN: 所有实时通知和会话流都复用同一个 Hub 入口。
// en: Reuse a single hub endpoint for real-time notifications and session streaming.
app.MapHub<NotificationHub>("/hubs/notification");

app.MapDefaultEndpoints();

app.Run();
