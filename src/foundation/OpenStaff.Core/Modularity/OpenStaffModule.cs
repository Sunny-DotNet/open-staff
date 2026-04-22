using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenStaff.Core.Modularity;

/// <summary>
/// 所有 OpenStaff 模块的基类 / Base class for all OpenStaff modules.
/// 每个类库项目实现一个模块类，通过 [DependsOn] 声明依赖，
/// 框架自动拓扑排序并依次调用生命周期方法。
/// Each library contributes a module type, declares dependencies with <see cref="DependsOnAttribute"/>, and participates in ordered lifecycle callbacks.
/// </summary>
public abstract class OpenStaffModule
{
    internal ServiceConfigurationContext? ServiceConfigurationContext { get; set; }

    /// <summary>
    /// 配置服务阶段：在此注册本模块的 DI 服务 / Register the module's services during application composition.
    /// </summary>
    /// <param name="context">服务配置上下文 / Service configuration context.</param>
    public virtual void ConfigureServices(ServiceConfigurationContext context) { }

    /// <summary>
    /// 应用初始化阶段：在此配置中间件、端点映射等 / Configure middleware, endpoints, and runtime behaviors during startup.
    /// </summary>
    /// <param name="context">应用初始化上下文 / Application initialization context.</param>
    public virtual void OnApplicationInitialization(ApplicationInitializationContext context) { }

    /// <summary>
    /// 配置强类型选项（等价于 services.Configure&lt;TOptions&gt;(action)）。
    /// 仅在 ConfigureServices 阶段内调用有效。
    /// Configure strongly typed options with an action. Valid only during <see cref="ConfigureServices(ServiceConfigurationContext)"/>.
    /// </summary>
    /// <param name="configureOptions">选项配置委托 / Options configuration delegate.</param>
    protected void Configure<TOptions>(Action<TOptions> configureOptions)
        where TOptions : class
    {
        var ctx = ServiceConfigurationContext
            ?? throw new InvalidOperationException("Configure<T> 只能在 ConfigureServices 阶段调用");
        ctx.Services.Configure(configureOptions);
    }

    /// <summary>
    /// 从配置节绑定强类型选项（等价于 services.Configure&lt;TOptions&gt;(section)）。
    /// 仅在 ConfigureServices 阶段内调用有效。
    /// Bind strongly typed options from a configuration section. Valid only during <see cref="ConfigureServices(ServiceConfigurationContext)"/>.
    /// </summary>
    /// <param name="sectionName">配置节名称 / Configuration section name.</param>
    protected void Configure<TOptions>(string sectionName)
        where TOptions : class
    {
        var ctx = ServiceConfigurationContext
            ?? throw new InvalidOperationException("Configure<T> 只能在 ConfigureServices 阶段调用");
        ctx.Services.AddOptions<TOptions>()
            .Bind(ctx.Configuration.GetSection(sectionName));
    }
}
