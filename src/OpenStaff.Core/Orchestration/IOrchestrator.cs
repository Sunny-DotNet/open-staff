using OpenStaff.Core.Agents;

namespace OpenStaff.Core.Orchestration;

/// <summary>
/// 编排器接口 / Orchestrator interface
/// </summary>
public interface IOrchestrator
{
    /// <summary>
    /// 处理用户输入 / Handle user input
    /// </summary>
    Task<AgentResponse> HandleUserInputAsync(Guid projectId, string input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 路由消息到指定角色 / Route message to a specific role
    /// </summary>
    Task<AgentResponse> RouteToAgentAsync(Guid projectId, string targetRole, AgentMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取工程中所有角色的状态 / Get status of all agents in a project
    /// </summary>
    Task<IReadOnlyList<AgentStatusInfo>> GetAgentStatusesAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 初始化工程的所有智能体 / Initialize all agents for a project
    /// </summary>
    Task InitializeProjectAgentsAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public class AgentStatusInfo
{
    public Guid AgentId { get; set; }
    public string RoleType { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CurrentTask { get; set; }
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;
}
