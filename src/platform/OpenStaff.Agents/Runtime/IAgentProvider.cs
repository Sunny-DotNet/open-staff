using OpenStaff.Agents;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;

namespace OpenStaff.Agent;

/// <summary>
/// zh-CN: 定义运行时智能体提供程序必须实现的统一入口。
/// en: Defines the unified entry point implemented by runtime agent providers.
/// </summary>
public interface IAgentProvider
{
    /// <summary>
    /// zh-CN: 获取提供程序标识，例如 builtin、anthropic 或 google。
    /// en: Gets the provider identifier, such as builtin, anthropic, or google.
    /// </summary>
    string ProviderType { get; }

    /// <summary>
    /// zh-CN: 获取用于界面展示的提供程序名称。
    /// en: Gets the provider display name shown in the UI.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// zh-CN: 获取提供程序头像资源；该元数据仅描述运行时提供程序本身。
    /// en: Gets the provider avatar resource; this metadata only describes the runtime provider itself.
    /// </summary>
    string? AvatarDataUri => null;

    /// <summary>
    /// zh-CN: 根据角色定义与运行时上下文创建智能体实例；账号解析由具体提供程序自行完成。
    /// en: Creates an agent instance from the role definition and runtime context; account resolution is owned by the concrete provider.
    /// </summary>
    Task<IStaffAgent> CreateAgentAsync(AgentRole role, AgentContext context);
}
