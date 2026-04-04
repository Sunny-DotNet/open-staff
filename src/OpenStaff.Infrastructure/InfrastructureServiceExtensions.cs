using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Events;
using OpenStaff.Core.Services;
using OpenStaff.Infrastructure.Events;
using OpenStaff.Infrastructure.Export;
using OpenStaff.Infrastructure.Git;
using OpenStaff.Infrastructure.LLM;
using OpenStaff.Infrastructure.Persistence;
using OpenStaff.Infrastructure.Plugins;
using OpenStaff.Infrastructure.Security;

namespace OpenStaff.Infrastructure;

/// <summary>
/// 基础设施层服务注册 / Infrastructure layer service registration
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, string? encryptionKey = null)
    {
        // 数据库 / Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // 加密服务 / Encryption service
        services.AddSingleton(new EncryptionService(encryptionKey ?? "OpenStaff-Default-Key-Change-In-Production"));

        // 事件系统 / Event system
        services.AddSingleton<EventBus>();
        services.AddSingleton<IEventSubscriber>(sp => sp.GetRequiredService<EventBus>());
        services.AddScoped<IEventPublisher, EventPublisher>();

        // LLM 客户端 / LLM clients
        services.AddHttpClient();
        services.AddSingleton<IModelClientFactory, ModelClientFactory>();

        // Git 服务 / Git service
        services.AddScoped<GitService>();

        // 导出/导入 / Export/Import
        services.AddScoped<ProjectExporter>();
        services.AddScoped<ProjectImporter>();

        // 插件加载 / Plugin loader
        services.AddSingleton<PluginLoader>();

        return services;
    }
}
