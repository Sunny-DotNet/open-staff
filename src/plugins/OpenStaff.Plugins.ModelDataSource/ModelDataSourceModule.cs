using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;

namespace OpenStaff.Plugins.ModelDataSource;

[DependsOn(typeof(OpenStaffCoreModule))]
public class ModelDataSourceModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 注册 models.dev 数据源（默认实现）
        services.AddSingleton<ModelsDevModelDataSource>();
        services.AddSingleton<IModelDataSource>(sp => sp.GetRequiredService<ModelsDevModelDataSource>());
    }
}
