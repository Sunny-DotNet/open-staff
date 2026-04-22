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

    /// <summary>执行工具 / Execute the tool with serialized arguments.</summary>
    /// <param name="arguments">调用参数（通常为 JSON） / Invocation arguments, usually encoded as JSON.</param>
    /// <param name="context">执行上下文 / Execution context.</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>工具输出文本 / Tool output as text.</returns>
    Task<string> ExecuteAsync(string arguments, AgentContext context, CancellationToken ct = default);
}
