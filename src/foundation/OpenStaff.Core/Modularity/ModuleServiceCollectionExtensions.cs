using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenStaff.Core.Modularity;

/// <summary>
/// 模块化 DI 扩展 / Dependency-injection extensions for loading and initializing OpenStaff modules.
/// </summary>
public static class ModuleServiceCollectionExtensions
{
    /// <summary>
    /// 加载并配置所有 OpenStaff 模块（从启动模块开始，自动解析依赖） / Load and configure all OpenStaff modules starting from the specified startup module.
    /// </summary>
    /// <param name="services">服务集合 / Service collection.</param>
    /// <param name="configuration">应用配置 / Application configuration.</param>
    /// <returns>原始服务集合 / The same service collection for chaining.</returns>
    /// <remarks>
    /// 该方法会在调用每个模块的 <c>ConfigureServices</c> 前临时设置上下文，并把模块实例列表注册到 DI，供后续初始化阶段复用。
    /// The method temporarily assigns the configuration context before invoking each module's <c>ConfigureServices</c>, then stores the module list in DI for the later initialization phase.
    /// </remarks>
    public static IServiceCollection AddOpenStaffModules<TStartupModule>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TStartupModule : OpenStaffModule
    {
        var startupPluginModules = StartupPluginModuleDiscovery.Discover(configuration);
        var modules = ModuleLoader.LoadModules<TStartupModule>(startupPluginModules);
        var context = new ServiceConfigurationContext(services, configuration);

        foreach (var module in modules)
        {
            module.ServiceConfigurationContext = context;
            module.ConfigureServices(context);
            module.ServiceConfigurationContext = null;
        }

        // zh-CN: 将模块实例存入 DI，供后续启动阶段复用，避免再次扫描和重新创建模块对象。
        // en: Store module instances in DI so startup can reuse the same ordered list without rescanning or recreating modules.
        services.AddSingleton<IReadOnlyList<OpenStaffModule>>(modules);
        services.AddSingleton<IReadOnlyList<Type>>(startupPluginModules);

        return services;
    }

    /// <summary>
    /// 执行所有模块的应用初始化（按拓扑排序顺序） / Run application initialization for all modules in dependency order.
    /// </summary>
    /// <param name="serviceProvider">应用服务提供器 / Application service provider.</param>
    /// <remarks>
    /// 该方法依赖 <see cref="AddOpenStaffModules{TStartupModule}(IServiceCollection, IConfiguration)"/> 预先注册的模块列表。
    /// This method depends on the module list that was registered earlier by <see cref="AddOpenStaffModules{TStartupModule}(IServiceCollection, IConfiguration)"/>.
    /// </remarks>
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
