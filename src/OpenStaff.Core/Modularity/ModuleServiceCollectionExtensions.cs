using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenStaff.Core.Modularity;

public static class ModuleServiceCollectionExtensions
{
    /// <summary>
    /// 加载并配置所有 OpenStaff 模块（从启动模块开始，自动解析依赖）。
    /// </summary>
    public static IServiceCollection AddOpenStaffModules<TStartupModule>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TStartupModule : OpenStaffModule
    {
        var modules = ModuleLoader.LoadModules<TStartupModule>();
        var context = new ServiceConfigurationContext(services, configuration);

        foreach (var module in modules)
        {
            module.ServiceConfigurationContext = context;
            module.ConfigureServices(context);
            module.ServiceConfigurationContext = null;
        }

        // 将模块实例存入 DI，供 UseOpenStaffModules 使用
        services.AddSingleton<IReadOnlyList<OpenStaffModule>>(modules);

        return services;
    }

    /// <summary>
    /// 执行所有模块的应用初始化（按拓扑排序顺序）。
    /// </summary>
    public static void UseOpenStaffModules(this IServiceProvider serviceProvider)
    {
        var modules = serviceProvider.GetRequiredService<IReadOnlyList<OpenStaffModule>>();
        var context = new ApplicationInitializationContext(serviceProvider);

        foreach (var module in modules)
        {
            module.OnApplicationInitialization(context);
        }
    }
}
