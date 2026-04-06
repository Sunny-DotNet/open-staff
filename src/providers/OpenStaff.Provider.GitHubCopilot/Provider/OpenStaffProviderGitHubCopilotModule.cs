using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Provider.Options;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Provider;

[DependsOn(typeof(ProviderAbstractionsModule))]
public class OpenStaffProviderGitHubCopilotModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<CopilotTokenService>();

        Configure<ProviderOptions>(options =>
        {
            options.AddProtocol<GitHubCopilotProtocol>();
        });
    }
}
