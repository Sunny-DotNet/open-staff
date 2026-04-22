using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;

namespace OpenStaff.Plugins.ModelDataSource;

/// <summary>
/// 模型数据源模块，注册默认的模型元数据提供者。
/// Model data source module that registers the default provider of model metadata.
/// </summary>
[DependsOn(typeof(OpenStaffCoreModule))]
public class ModelDataSourceModule : OpenStaffModule
{
    /// <summary>
    /// 配置模型数据源相关服务。
    /// Configures services related to model data sources.
    /// </summary>
    /// <param name="context">
    /// 模块服务配置上下文。
    /// Module service configuration context.
    /// </param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // zh-CN: 默认实现直接暴露 Models.dev 数据源，并将接口解析指向同一个单例实例。
        // en: The default implementation exposes the Models.dev data source and resolves the interface to the same singleton instance.
        services.AddSingleton<ModelsDevModelDataSource>();
        services.AddSingleton<IModelDataSource>(sp => sp.GetRequiredService<ModelsDevModelDataSource>());
    }
}
