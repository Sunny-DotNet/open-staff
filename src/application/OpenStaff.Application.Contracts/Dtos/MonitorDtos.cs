namespace OpenStaff.Dtos;

/// <summary>
/// 全局监控统计。
/// Global monitoring statistics.
/// </summary>
public class SystemStatsDto
{
    /// <summary>项目总数。 / Total number of projects.</summary>
    public int Projects { get; set; }

    /// <summary>智能体总数。 / Total number of agents.</summary>
    public int Agents { get; set; }

    /// <summary>任务总数。 / Total number of tasks.</summary>
    public int Tasks { get; set; }

    /// <summary>事件总数。 / Total number of events.</summary>
    public int Events { get; set; }

    /// <summary>已完成任务数。 / Number of completed tasks.</summary>
    public int CompletedTasks { get; set; }

    /// <summary>会话总数。 / Total number of sessions.</summary>
    public int Sessions { get; set; }

    /// <summary>模型提供商数量。 / Number of model providers.</summary>
    public int ModelProviders { get; set; }

    /// <summary>角色数量。 / Number of agent roles.</summary>
    public int AgentRoles { get; set; }

    /// <summary>最近会话列表。 / Recently created sessions.</summary>
    public List<RecentSessionDto> RecentSessions { get; set; } = [];
}

/// <summary>
/// 近期会话摘要。
/// Summary information for a recent session.
/// </summary>
public class RecentSessionDto
{
    /// <summary>会话标识。 / Session identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>项目名称。 / Project name.</summary>
    public string? ProjectName { get; set; }

    /// <summary>会话状态。 / Session status.</summary>
    public string? Status { get; set; }

    /// <summary>会话场景。 / Session scene.</summary>
    public string? Scene { get; set; }

    /// <summary>会话输入摘要。 / Session input summary.</summary>
    public string? Input { get; set; }

    /// <summary>创建时间（UTC）。 / Creation time in UTC.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 单个项目的监控统计。
/// Monitoring statistics for a single project.
/// </summary>
public class ProjectStatsDto
{
    /// <summary>项目智能体列表。 / Project agent summaries.</summary>
    public List<ProjectAgentDto> Agents { get; set; } = [];

    /// <summary>按状态汇总的任务数量。 / Task counts grouped by status.</summary>
    public Dictionary<string, int> TasksByStatus { get; set; } = [];

    /// <summary>按类型汇总的事件数量。 / Event counts grouped by type.</summary>
    public Dictionary<string, int> EventsByType { get; set; } = [];

    /// <summary>按场景拆分的统计。 / Statistics broken down by scene.</summary>
    public List<SceneBreakdownDto> SceneBreakdown { get; set; } = [];

    /// <summary>近期事件列表。 / Recent events.</summary>
    public List<EventDto> RecentEvents { get; set; } = [];

    /// <summary>检查点数量。 / Number of checkpoints.</summary>
    public int Checkpoints { get; set; }
}

/// <summary>
/// 项目智能体摘要。
/// Summary information for a project agent in monitoring views.
/// </summary>
public class ProjectAgentDto
{
    /// <summary>项目智能体标识。 / Project agent identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>角色名称。 / Role name.</summary>
    public string? RoleName { get; set; }

    /// <summary>当前状态。 / Current status.</summary>
    public string? Status { get; set; }
}

/// <summary>
/// 场景维度统计。
/// Statistics grouped by scene.
/// </summary>
public class SceneBreakdownDto
{
    /// <summary>场景名称。 / Scene name.</summary>
    public string Scene { get; set; } = string.Empty;

    /// <summary>会话数量。 / Number of sessions.</summary>
    public int SessionCount { get; set; }

    /// <summary>任务数量。 / Number of tasks.</summary>
    public int TaskCount { get; set; }

    /// <summary>事件数量。 / Number of events.</summary>
    public int EventCount { get; set; }

    /// <summary>运行次数。 / Number of execution runs.</summary>
    public int RunCount { get; set; }

    /// <summary>总 Token 数。 / Total token count.</summary>
    public int TotalTokens { get; set; }

    /// <summary>平均耗时（毫秒）。 / Average duration in milliseconds.</summary>
    public long? AverageDurationMs { get; set; }
}

/// <summary>
/// 监控事件投影。
/// Projected monitoring event.
/// </summary>
public class EventDto
{
    /// <summary>事件唯一标识。 / Unique event identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>事件类型。 / Event type.</summary>
    public string? EventType { get; set; }

    /// <summary>原始数据。 / Raw event data.</summary>
    public string? Data { get; set; }

    /// <summary>展示内容。 / Display content.</summary>
    public string? Content { get; set; }

    /// <summary>扩展元数据 JSON。 / Additional metadata JSON.</summary>
    public string? Metadata { get; set; }

    /// <summary>关联任务标识。 / Associated task identifier.</summary>
    public Guid? TaskId { get; set; }

    /// <summary>关联会话标识。 / Associated session identifier.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>关联帧标识。 / Associated frame identifier.</summary>
    public Guid? FrameId { get; set; }

    /// <summary>关联消息标识。 / Associated message identifier.</summary>
    public Guid? MessageId { get; set; }

    /// <summary>关联执行包标识。 / Associated execution-package identifier.</summary>
    public Guid? ExecutionPackageId { get; set; }

    /// <summary>关联项目内角色关联标识。 / Associated project-scoped role membership identifier.</summary>
    public Guid? ProjectAgentRoleId { get; set; }

    /// <summary>智能体名称。 / Agent name.</summary>
    public string? AgentName { get; set; }

    /// <summary>场景名称。 / Scene name.</summary>
    public string? Scene { get; set; }

    /// <summary>入口类型。 / Business entry kind.</summary>
    public string? EntryKind { get; set; }

    /// <summary>角色标识。 / Role identifier.</summary>
    public Guid? AgentRoleId { get; set; }

    /// <summary>目标角色标识。 / Target role identifier.</summary>
    public Guid? TargetAgentRoleId { get; set; }

    /// <summary>目标项目内角色关联标识。 / Target project-scoped role membership identifier.</summary>
    public Guid? TargetProjectAgentRoleId { get; set; }

    /// <summary>使用的模型。 / Model used for the event.</summary>
    public string? Model { get; set; }

    /// <summary>工具名称。 / Tool name.</summary>
    public string? ToolName { get; set; }

    /// <summary>工具调用标识。 / Tool call identifier.</summary>
    public string? ToolCallId { get; set; }

    /// <summary>事件状态。 / Event status.</summary>
    public string? Status { get; set; }

    /// <summary>来源帧标识。 / Source frame identifier.</summary>
    public Guid? SourceFrameId { get; set; }

    /// <summary>来源 effect 序号。 / Source effect index.</summary>
    public int? SourceEffectIndex { get; set; }

    /// <summary>补充说明。 / Additional detail.</summary>
    public string? Detail { get; set; }

    /// <summary>尝试次数。 / Attempt count.</summary>
    public int? Attempt { get; set; }

    /// <summary>最大尝试次数。 / Maximum attempt count.</summary>
    public int? MaxAttempts { get; set; }

    /// <summary>总 Token 数。 / Total token count.</summary>
    public int? TotalTokens { get; set; }

    /// <summary>总耗时（毫秒）。 / Total duration in milliseconds.</summary>
    public long? DurationMs { get; set; }

    /// <summary>首 Token 延迟（毫秒）。 / First-token latency in milliseconds.</summary>
    public long? FirstTokenMs { get; set; }

    /// <summary>创建时间（UTC）。 / Creation time in UTC.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 查询项目事件流的请求。
/// Request used to query project events.
/// </summary>
public class GetEventsRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>页码。 / Page number.</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页大小。 / Page size.</summary>
    public int PageSize { get; set; } = 50;

    /// <summary>可选的事件类型过滤条件。 / Optional event type filter.</summary>
    public string? EventType { get; set; }

    /// <summary>可选的场景过滤条件。 / Optional scene filter.</summary>
    public string? Scene { get; set; }
}
