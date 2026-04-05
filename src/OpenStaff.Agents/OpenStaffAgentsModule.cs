using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agents.Prompts;
using OpenStaff.Agents.Tools;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Modularity;

namespace OpenStaff.Agents;

[DependsOn(typeof(OpenStaffCoreModule))]
public class OpenStaffAgentsModule : OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 智能体工具注册表
        services.AddSingleton<IAgentToolRegistry, AgentToolRegistry>();

        // 提示词加载器
        services.AddSingleton<IPromptLoader, EmbeddedPromptLoader>();

        // ChatClient 工厂 — 根据 ModelProtocolType 创建对应的 IChatClient
        services.AddSingleton<ChatClientFactory>();

        // AI Agent 工厂 — 使用 microsoft/agent-framework 创建 AIAgent
        services.AddSingleton<AIAgentFactory>();

        // 智能体工厂 — 角色注册在 RoleSeedService 启动时完成
        services.AddSingleton<AgentFactory>(sp =>
        {
            var toolRegistry = sp.GetRequiredService<IAgentToolRegistry>();
            var aiAgentFactory = sp.GetRequiredService<AIAgentFactory>();
            return new AgentFactory(sp, toolRegistry, aiAgentFactory);
        });
    }
}
