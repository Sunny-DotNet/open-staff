using Microsoft.Extensions.AI;
using OpenStaff.Core.Agents;

namespace OpenStaff.Agents.Tools;

/// <summary>
/// IAgentTool → AITool 桥接
/// 将 OpenStaff 自定义工具接口转换为 Microsoft.Extensions.AI 标准 AITool
/// </summary>
public static class AgentToolBridge
{
    /// <summary>
    /// 将 IAgentTool 转换为 AIFunction (AITool 子类)
    /// </summary>
    public static AIFunction ToAIFunction(IAgentTool tool, AgentContext context)
    {
        return AIFunctionFactory.Create(
            method: async (string arguments) =>
            {
                return await tool.ExecuteAsync(arguments, context);
            },
            name: tool.Name,
            description: tool.Description);
    }

    /// <summary>
    /// 批量转换工具列表
    /// </summary>
    public static IList<AITool> ToAITools(IEnumerable<IAgentTool> tools, AgentContext context)
    {
        return tools.Select(t => (AITool)ToAIFunction(t, context)).ToList();
    }
}
