using Microsoft.Extensions.DependencyInjection;
using OpenStaff.AgentSouls.Services;
using OpenStaff.Core.Modularity;

namespace OpenStaff.AgentSouls;

[DependsOn(typeof(OpenStaffCoreModule))]
public class OpenStaffAgentSoulsModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClient(nameof(AgentSouls), client =>
        {
            client.BaseAddress = new Uri("https://agent-souls.open-hub.cc/");
        });

        context.Services
            .AddSingleton<PersonalityTraitsHttpService>()
            .AddSingleton<WorkAttitudesHttpService>()
            .AddSingleton<CommunicationStylesHttpService>()
            .AddSingleton<IAgentSoulService, AgentSoulService>();
    }
}
