using Microsoft.EntityFrameworkCore;
using OpenStaff.Api;
using OpenStaff.Api.Hubs;
using OpenStaff.Api.Middleware;
using OpenStaff.Application.Models;
using OpenStaff.Core.Modularity;
using OpenStaff.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Aspire 服务默认值 / Aspire service defaults
builder.AddServiceDefaults();

// 模块化加载 / Modular loading
builder.Services.AddOpenStaffModules<OpenStaffApiModule>(builder.Configuration);

var app = builder.Build();

// 模块初始化 / Module initialization
app.Services.UseOpenStaffModules();

// 自动迁移数据库 / Auto-migrate database on startup
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// 初始化 models.dev 数据
await app.Services.GetRequiredService<ModelsDevService>().InitializeAsync();

// 中间件 / Middleware
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<LocaleMiddleware>();

app.UseCors();
app.MapControllers();

// SignalR — 统一通知 Hub
app.MapHub<NotificationHub>("/hubs/notification");

// Aspire 健康检查端点
app.MapDefaultEndpoints();

app.Run();
