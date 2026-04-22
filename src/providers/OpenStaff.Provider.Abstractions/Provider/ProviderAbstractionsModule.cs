using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider.Options;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Provider;

/// <summary>
/// Provider 抽象模块，注册协议发现与实例化所需的基础服务。
/// Provider abstraction module that registers the core services used for protocol discovery and instantiation.
/// </summary>
[DependsOn(typeof(ModelDataSourceModule))]
public class ProviderAbstractionsModule : OpenStaffModule
{
    /// <summary>
    /// 配置 Provider 抽象层服务与选项。
    /// Configures provider abstraction services and options.
    /// </summary>
    /// <param name="context">
    /// 模块服务配置上下文。
    /// Module service configuration context.
    /// </param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<ProviderOptions>(options => { });

        // zh-CN: 抽象层只注册协议工厂，具体协议类型由各 Provider 模块按需补充。
        // en: The abstraction layer only registers the protocol factory; concrete protocol types are added by each provider module.
        context.Services.AddSingleton<IPlatformRegistry, PlatformRegistry>();
        context.Services.AddSingleton<IProtocolFactory, ProtocolFactory>();

        context.Services.AddTransient<ICurrentProviderDetail, CurrentProviderDetail>();
    }
}
