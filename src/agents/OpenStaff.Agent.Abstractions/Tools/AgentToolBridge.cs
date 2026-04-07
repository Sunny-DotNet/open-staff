using Microsoft.Extensions.AI;
using OpenStaff.Core.Agents;

namespace OpenStaff.Agent.Tools;

/// <summary>
/// IAgentTool → AITool 桥接
/// </summary>
public static class AgentToolBridge
{
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

    public static IList<AITool> ToAITools(IEnumerable<IAgentTool> tools, AgentContext context)
    {
        return tools.Select(t => (AITool)ToAIFunction(t, context)).ToList();
    }
}
