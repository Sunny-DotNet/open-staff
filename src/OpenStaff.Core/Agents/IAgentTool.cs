namespace OpenStaff.Core.Agents;

/// <summary>
/// 智能体工具接口 / Agent tool interface
/// </summary>
public interface IAgentTool
{
    /// <summary>工具名称 / Tool name (e.g., "create_file")</summary>
    string Name { get; }

    /// <summary>工具描述 / Tool description for LLM</summary>
    string Description { get; }

    /// <summary>参数 JSON Schema / Parameters JSON schema for function calling</summary>
    string ParametersSchema { get; }

    /// <summary>执行工具 / Execute tool with given arguments</summary>
    Task<string> ExecuteAsync(string arguments, AgentContext context, CancellationToken ct = default);
}
