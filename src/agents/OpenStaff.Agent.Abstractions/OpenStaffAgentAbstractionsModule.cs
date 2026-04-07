using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent.Tools;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Modularity;

namespace OpenStaff.Agent;

[DependsOn(typeof(OpenStaffCoreModule))]
public class OpenStaffAgentAbstractionsModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 工具注册表
        services.AddSingleton<IAgentToolRegistry, AgentToolRegistry>();

        // 智能体工厂 — 注入所有 IAgentProvider
        services.AddSingleton<AgentFactory>(sp =>
            new AgentFactory(sp.GetServices<IAgentProvider>()));
    }
}
