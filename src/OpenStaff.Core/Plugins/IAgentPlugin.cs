using Microsoft.Agents.AI;
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
    AIAgent CreateAgent(AgentContext context);

    /// <summary>
    /// 获取此插件提供的工具 / Get tools provided by this plugin
    /// </summary>
    IEnumerable<IAgentTool> GetTools();
}
