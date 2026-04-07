namespace OpenStaff.Core.Orchestration;

/// <summary>
/// 编排器接口 / Orchestrator interface
/// </summary>
public interface IOrchestrator
{
    Task<OrchestrationResponse> HandleUserInputAsync(Guid projectId, string input, CancellationToken cancellationToken = default);
    Task<OrchestrationResponse> RouteToAgentAsync(Guid projectId, string targetRole, string message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentStatusInfo>> GetAgentStatusesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task InitializeProjectAgentsAsync(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 编排响应 / Orchestration response
/// </summary>
public class OrchestrationResponse
{
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? TargetRole { get; set; }
    public bool RequiresUserInput { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public List<string> Errors { get; set; } = new();
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
