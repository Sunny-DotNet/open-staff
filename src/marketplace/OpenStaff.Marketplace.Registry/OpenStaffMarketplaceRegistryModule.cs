using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Marketplace.Options;

namespace OpenStaff.Marketplace.Registry;

[DependsOn(typeof(MarketplaceAbstractionsModule))]
public class OpenStaffMarketplaceRegistryModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 注册 HttpClient for Registry API
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
