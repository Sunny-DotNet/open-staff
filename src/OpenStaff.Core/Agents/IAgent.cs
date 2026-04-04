namespace OpenStaff.Core.Agents;

using OpenStaff.Core.Models;

/// <summary>
/// 智能体接口 / Agent interface
/// </summary>
public interface IAgent
{
    /// <summary>角色类型标识 / Role type identifier</summary>
    string RoleType { get; }

    /// <summary>当前状态 / Current status</summary>
    string Status { get; }

    /// <summary>
    /// 初始化智能体 / Initialize the agent
    /// </summary>
    Task InitializeAsync(AgentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理消息 / Process a message
    /// </summary>
    Task<AgentResponse> ProcessAsync(AgentMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止智能体 / Stop the agent
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
