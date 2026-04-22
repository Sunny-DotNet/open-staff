namespace OpenStaff.Core.Orchestration;

/// <summary>
/// 项目智能体运行时协调接口 / Project agent runtime coordination interface
/// </summary>
public interface IOrchestrator
{
    /// <summary>获取项目内智能体的运行状态 / Get runtime status for agents in a project.</summary>
    /// <param name="projectId">项目标识 / Project identifier.</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token.</param>
    /// <returns>智能体状态列表 / Agent status list.</returns>
    Task<IReadOnlyList<AgentStatusInfo>> GetAgentStatusesAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>初始化项目级智能体实例 / Initialize project-scoped agent instances.</summary>
    /// <param name="projectId">项目标识 / Project identifier.</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token.</param>
    Task InitializeProjectAgentsAsync(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 编排响应 / Orchestration response
/// </summary>
public class OrchestrationResponse
{
    /// <summary>是否成功 / Whether orchestration succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>主响应内容 / Primary response content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>建议下一跳角色 / Suggested next role.</summary>
    public string? TargetRole { get; set; }

    /// <summary>是否需要用户继续输入 / Whether more user input is required.</summary>
    public bool RequiresUserInput { get; set; }

    /// <summary>使用的模型 / Model used to produce the response.</summary>
    public string? Model { get; set; }

    /// <summary>Token 用量信息 / Token usage details.</summary>
    public OrchestrationUsage? Usage { get; set; }

    /// <summary>时延信息 / Timing details.</summary>
    public OrchestrationTiming? Timing { get; set; }

    /// <summary>扩展数据 / Additional structured data.</summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>错误列表 / Collected error messages.</summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 编排用量信息 / Token usage metrics for orchestration.
/// </summary>
public class OrchestrationUsage
{
    /// <summary>输入 Token 数 / Input token count.</summary>
    public int? InputTokens { get; set; }

    /// <summary>输出 Token 数 / Output token count.</summary>
    public int? OutputTokens { get; set; }

    /// <summary>总 Token 数 / Total token count.</summary>
    public int? TotalTokens { get; set; }
}

/// <summary>
/// 编排时延信息 / Timing metrics for orchestration.
/// </summary>
public class OrchestrationTiming
{
    /// <summary>总耗时（毫秒） / Total duration in milliseconds.</summary>
    public long? TotalMs { get; set; }

    /// <summary>首个 Token 延迟（毫秒） / First-token latency in milliseconds.</summary>
    public long? FirstTokenMs { get; set; }
}

/// <summary>
/// 智能体状态快照 / Runtime status snapshot for a project agent.
/// </summary>
public class AgentStatusInfo
{
    /// <summary>智能体实例标识 / Agent instance identifier.</summary>
    public Guid AgentId { get; set; }

    /// <summary>角色类型 / Role type.</summary>
    public string RoleType { get; set; } = string.Empty;

    /// <summary>角色名称 / Role display name.</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>当前状态 / Current status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>当前任务摘要 / Current task summary.</summary>
    public string? CurrentTask { get; set; }

    /// <summary>最后使用时间（UTC） / Last-used timestamp in UTC.</summary>
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;
}
