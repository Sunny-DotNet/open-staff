using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Provider;

[DependsOn(typeof(ModelDataSourceModule))]
public class ProviderAbstractionsModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 抽象层无需注册具体服务，具体 Provider 实现各自注册
        context.Services.AddSingleton<ProtocolFactory>();
        
    }
}
