using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenStaff.Core.Modularity;

public static class ModuleServiceCollectionExtensions
{
    private const string ModulesKey = "__OpenStaffModules__";

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
            module.ConfigureServices(context);
        }

        // 将模块实例存入 DI，供 UseOpenStaffModules 使用
        services.AddSingleton<IReadOnlyList<OpenStaffModule>>(modules);

        return services;
    }
}
