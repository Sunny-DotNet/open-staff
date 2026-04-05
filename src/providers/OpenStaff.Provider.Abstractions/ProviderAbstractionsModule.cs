using OpenStaff.Core.Modularity;
using OpenStaff.Plugins.ModelDataSource;

namespace OpenStaff.Provider;

[DependsOn(typeof(ModelDataSourceModule))]
public class ProviderAbstractionsModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 抽象层无需注册具体服务，具体 Provider 实现各自注册
    }
}
