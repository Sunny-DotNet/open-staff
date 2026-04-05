using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider.Options;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Provider;

[DependsOn(typeof(ModelDataSourceModule))]
public class ProviderAbstractionsModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<ProviderOptions>(options => { });
        // 抽象层无需注册具体服务，具体 Provider 实现各自注册
        context.Services.AddSingleton<IProtocolFactory,ProtocolFactory>();
        
    }
}
