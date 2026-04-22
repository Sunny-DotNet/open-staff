using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Modularity;
using OpenStaff.Provider;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Agent.Builtin;

/// <summary>
/// zh-CN: 注册内置智能体提供程序及其依赖。
/// en: Registers the builtin agent provider and its supporting services.
/// </summary>
[DependsOn(typeof(OpenStaffAgentAbstractionsModule), typeof(ProviderAbstractionsModule))]
public class OpenStaffAgentBuiltinModule : OpenStaffModule
{
    /// <summary>
    /// zh-CN: 将聊天客户端工厂和内置提供程序加入依赖注入容器。
    /// en: Adds the chat-client factory and builtin provider to dependency injection.
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddSingleton(sp => new ChatClientFactory(
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<IProtocolFactory>(),
            sp.GetRequiredService<IPlatformRegistry>(),
            sp));
        services.AddSingleton<BuiltinAgentProvider>();
        // zh-CN: BuiltinAgentProvider 同时以具体类型和 IAgentProvider 暴露，方便内部扩展与统一工厂共用。
        // en: Expose BuiltinAgentProvider both as its concrete type and as IAgentProvider so internal extensions and the shared factory can reuse the same singleton.
        services.AddSingleton<IAgentProvider>(sp => sp.GetRequiredService<BuiltinAgentProvider>());
    }
}
