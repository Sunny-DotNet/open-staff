using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agents.Prompts;
using OpenStaff.Agents.Tools;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Modularity;
using OpenStaff.Vendor;

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

        // ChatClient 工厂 — 仅处理 OpenAI 兼容协议
        services.AddSingleton<ChatClientFactory>();

        // AI Agent 工厂 — 使用 microsoft/agent-framework 创建 AIAgent
        services.AddSingleton<AIAgentFactory>();

        // 智能体工厂 — 注入 VendorProviders
        services.AddSingleton<AgentFactory>(sp =>
        {
            var toolRegistry = sp.GetRequiredService<IAgentToolRegistry>();
            var aiAgentFactory = sp.GetRequiredService<AIAgentFactory>();
            var vendorProviders = sp.GetServices<IVendorAgentProvider>();
            return new AgentFactory(sp, toolRegistry, aiAgentFactory, vendorProviders);
        });
    }
}
