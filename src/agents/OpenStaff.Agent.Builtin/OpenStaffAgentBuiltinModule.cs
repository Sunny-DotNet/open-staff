using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent.Builtin.Prompts;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Modularity;

namespace OpenStaff.Agent.Builtin;

[DependsOn(typeof(OpenStaffAgentAbstractionsModule))]
public class OpenStaffAgentBuiltinModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddSingleton<IPromptLoader, EmbeddedPromptLoader>();
        services.AddSingleton<ChatClientFactory>();
        services.AddSingleton<BuiltinAgentProvider>();
        services.AddSingleton<IAgentProvider>(sp => sp.GetRequiredService<BuiltinAgentProvider>());
    }
}
