using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Infrastructure.Export;
using OpenStaff.Infrastructure.Git;
using OpenStaff.Infrastructure.Persistence;
using OpenStaff.Infrastructure.Plugins;
using OpenStaff.Infrastructure.Security;

namespace OpenStaff.Infrastructure;

[DependsOn(typeof(OpenStaffCoreModule))]
public class OpenStaffInfrastructureModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Configuration;

        // 数据库连接串
        var staffDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".staff");
        Directory.CreateDirectory(staffDir);
        var defaultDbPath = Path.Combine(staffDir, "openstaff.db");

        var connectionString = configuration.GetConnectionString("openstaff")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? $"Data Source={defaultDbPath}";

        // 数据库 (SQLite)
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        // 加密服务
        var encryptionKey = configuration["Security:EncryptionKey"];
        services.AddSingleton(new EncryptionService(
            encryptionKey ?? "OpenStaff-Default-Key-Change-In-Production"));

        // HTTP 客户端
        services.AddHttpClient();

        // Git 服务
        services.AddScoped<GitService>();

        // 导出/导入
        services.AddScoped<ProjectExporter>();
        services.AddScoped<ProjectImporter>();

        // 插件加载
        services.AddSingleton<PluginLoader>();
    }
}
