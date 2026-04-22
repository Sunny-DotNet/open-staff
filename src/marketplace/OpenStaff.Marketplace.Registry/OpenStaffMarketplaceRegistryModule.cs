using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Marketplace.Options;

namespace OpenStaff.Marketplace.Registry;

/// <summary>
/// 官方 Registry 市场模块，注册远程 Registry API 客户端与市场源。
/// Official registry marketplace module that registers the remote registry API client and marketplace source.
/// </summary>
[DependsOn(typeof(MarketplaceAbstractionsModule))]
public class OpenStaffMarketplaceRegistryModule : OpenStaffModule
{
    /// <summary>
    /// 配置 Registry 市场源所需服务。
    /// Configures the services required by the registry marketplace source.
    /// </summary>
    /// <param name="context">
    /// 模块服务配置上下文。
    /// Module service configuration context.
    /// </param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // zh-CN: 为官方 Registry 配置专用 HttpClient，统一超时和 User-Agent，便于后续排查远程调用问题。
        // en: Configure a dedicated HttpClient for the official registry so timeout and User-Agent settings stay consistent for troubleshooting.
        context.Services.AddHttpClient<RegistryApiClient>(client =>
        {
            client.BaseAddress = new Uri(RegistryApiClient.DefaultBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("User-Agent", "OpenStaff/1.0");
        });

        Configure<MarketplaceOptions>(options =>
        {
            options.AddSource<RegistryMcpSource>();
        });
    }
}
