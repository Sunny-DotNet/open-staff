using OpenStaff.Agents;
using OpenStaff.Core.Agents;

namespace OpenStaff.Core.Plugins;

/// <summary>
/// 角色插件接口 / Agent plugin interface
/// </summary>
public interface IAgentPlugin : IPlugin
{
    /// <summary>
    /// 创建智能体实例 / Create agent instance
    /// </summary>
    /// <param name="context">智能体上下文 / Agent context.</param>
    /// <returns>创建后的 Staff 智能体 / Created staff agent.</returns>
    IStaffAgent CreateAgent(AgentContext context);
}
