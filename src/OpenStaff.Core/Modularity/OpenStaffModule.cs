using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenStaff.Core.Modularity;

/// <summary>
/// 所有 OpenStaff 模块的基类。
/// 每个类库项目实现一个模块类，通过 [DependsOn] 声明依赖，
/// 框架自动拓扑排序并依次调用生命周期方法。
/// </summary>
public abstract class OpenStaffModule
{
    internal ServiceConfigurationContext? ServiceConfigurationContext { get; set; }

    /// <summary>
    /// 配置服务阶段：在此注册本模块的 DI 服务。
    /// </summary>
    public virtual void ConfigureServices(ServiceConfigurationContext context) { }

    /// <summary>
    /// 应用初始化阶段：在此配置中间件、端点映射等。
    /// </summary>
    public virtual void OnApplicationInitialization(ApplicationInitializationContext context) { }

    /// <summary>
    /// 配置强类型选项（等价于 services.Configure&lt;TOptions&gt;(action)）。
    /// 仅在 ConfigureServices 阶段内调用有效。
    /// </summary>
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
    /// </summary>
    protected void Configure<TOptions>(string sectionName)
        where TOptions : class
    {
        var ctx = ServiceConfigurationContext
            ?? throw new InvalidOperationException("Configure<T> 只能在 ConfigureServices 阶段调用");
        ctx.Services.AddOptions<TOptions>()
            .Bind(ctx.Configuration.GetSection(sectionName));
    }
}
