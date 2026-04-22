using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.TalentMarket.Services;

namespace OpenStaff.TalentMarket;

[DependsOn(typeof(OpenStaffCoreModule))]
public class OpenStaffTalentMarketModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClient(TalentMarketRemoteCatalogService.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://raw.githubusercontent.com/Sunny-DotNet/agents/main/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenStaff");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        context.Services.AddSingleton<ITalentMarketRemoteCatalogService, TalentMarketRemoteCatalogService>();
    }
}
