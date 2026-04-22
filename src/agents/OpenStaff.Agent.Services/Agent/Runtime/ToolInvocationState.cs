namespace OpenStaff.Agent.Services;

/// <summary>
/// zh-CN: 在提供程序响应仍在流式输出时跟踪工具调用的可变状态。
/// en: Tracks the mutable state of a tool call while a provider response is still streaming.
/// </summary>
internal sealed class ToolInvocationState
{
    public required string CallId { get; init; }

    public required string Name { get; set; }

    public string? Arguments { get; set; }

    public string? Result { get; set; }

    public string? Error { get; set; }

    public ToolInvocationStatus Status { get; set; }
}
