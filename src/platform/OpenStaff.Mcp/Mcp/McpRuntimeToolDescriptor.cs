using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace OpenStaff.Mcp;

/// <summary>
/// Normalized MCP runtime tool contract that works for both external MCP tools and embedded builtin tools.
/// </summary>
public sealed record McpRuntimeToolDescriptor(
    AITool Tool,
    string? InputSchema = null)
{
    public string Name => Tool.Name;

    public string Description => Tool.Description;

    public static McpRuntimeToolDescriptor FromMcpClientTool(McpClientTool tool)
        => new(tool, tool.JsonSchema.ToString());

    public static McpRuntimeToolDescriptor FromAITool(AITool tool)
        => tool is AIFunction function
            ? new(tool, function.AsDeclarationOnly().JsonSchema.ToString())
            : new(tool);
}
