namespace OpenStaff.Agent.Services;

/// <summary>
/// zh-CN: 配置进程内智能体运行时的重试行为。
/// en: Configures retry behavior for the in-process agent runtime.
/// </summary>
public sealed record AgentServiceOptions
{
    /// <summary>
    /// zh-CN: 获取单条逻辑消息允许的最大尝试次数。
    /// en: Gets the maximum number of attempts allowed for a single logical message.
    /// </summary>
    public int MaxRetryCount { get; init; } = 1;
}
