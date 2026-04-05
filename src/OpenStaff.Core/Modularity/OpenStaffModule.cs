namespace OpenStaff.Core.Modularity;

/// <summary>
/// 所有 OpenStaff 模块的基类。
/// 每个类库项目实现一个模块类，通过 [DependsOn] 声明依赖，
/// 框架自动拓扑排序并依次调用生命周期方法。
/// </summary>
public abstract class OpenStaffModule
{
    /// <summary>
    /// 配置服务阶段：在此注册本模块的 DI 服务。
    /// </summary>
    public virtual void ConfigureServices(ServiceConfigurationContext context) { }

    /// <summary>
    /// 应用初始化阶段：在此配置中间件、端点映射等。
    /// </summary>
    public virtual void OnApplicationInitialization(ApplicationInitializationContext context) { }
}
