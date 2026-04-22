using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Modularity;

namespace OpenStaff.Agent;

/// <summary>
/// zh-CN: 注册智能体抽象层的核心单例服务。
/// en: Registers the core singleton services used by the agent abstraction layer.
/// </summary>
[DependsOn(typeof(OpenStaffCoreModule))]
public class OpenStaffAgentAbstractionsModule : OpenStaffModule
{
    /// <summary>
    /// zh-CN: 将提示词生成器加入依赖注入容器。
    /// en: Adds the prompt generator to dependency injection.
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddSingleton<IAgentPromptGenerator, AgentPromptGenerator>();
    }
}
