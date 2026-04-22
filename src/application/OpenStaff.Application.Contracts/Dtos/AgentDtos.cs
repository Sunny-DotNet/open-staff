
namespace OpenStaff.Dtos;

/// <summary>
/// 项目内角色关联摘要信息。
/// Summary information for a project-scoped role membership.
/// </summary>
public class AgentDto
{
    /// <summary>项目内角色关联标识。 / Project-scoped role membership identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>所属项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>角色定义标识。 / Agent role identifier.</summary>
    public Guid AgentRoleId { get; set; }


    /// <summary>角色显示名称。 / Role display name.</summary>
    public string? RoleName { get; set; }

    /// <summary>当前状态。 / Current agent status.</summary>
    public string? Status { get; set; }

    /// <summary>当前任务摘要。 / Current task summary.</summary>
    public string? CurrentTask { get; set; }

    /// <summary>关联角色摘要。 / Related role summary.</summary>
    public AgentRoleSummaryDto? AgentRole { get; set; }
}

/// <summary>
/// 项目内角色关联的角色摘要信息。
/// Summary information about the role attached to a project-scoped role membership.
/// </summary>
public class AgentRoleSummaryDto
{
    /// <summary>角色标识。 / Role identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>角色名称。 / Role name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>角色描述。 / Role description.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// 智能体事件分页结果。
/// Paged result for agent events.
/// </summary>
public class PagedAgentEventsDto
{
    /// <summary>当前页事件项。 / Event items for the current page.</summary>
    public List<AgentEventDto> Items { get; set; } = [];

    /// <summary>总记录数。 / Total number of records.</summary>
    public int Total { get; set; }
}

/// <summary>
/// 智能体运行事件投影。
/// Projected runtime event for an agent.
/// </summary>
public class AgentEventDto
{
    /// <summary>事件唯一标识。 / Unique event identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>事件类型。 / Event type.</summary>
    public string? EventType { get; set; }

    /// <summary>原始事件数据。 / Raw event data.</summary>
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

    /// <summary>场景名称。 / Scene name.</summary>
    public string? Scene { get; set; }

    /// <summary>入口类型。 / Business entry kind.</summary>
    public string? EntryKind { get; set; }

    /// <summary>执行角色标识。 / Executing role identifier.</summary>
    public Guid? AgentRoleId { get; set; }

    /// <summary>执行项目内角色关联标识。 / Executing project-scoped role membership identifier.</summary>
    public Guid? ProjectAgentRoleId { get; set; }

    /// <summary>目标角色标识。 / Target role identifier.</summary>
    public Guid? TargetAgentRoleId { get; set; }

    /// <summary>目标项目内角色关联标识。 / Target project-scoped role membership identifier.</summary>
    public Guid? TargetProjectAgentRoleId { get; set; }

    /// <summary>使用的模型。 / Model used by the event.</summary>
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
/// 设置项目智能体分配的请求。
/// Request used to update project agent assignments.
/// </summary>
public class SetProjectAgentsRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>角色标识列表。 / Agent role identifiers to assign.</summary>
    public List<Guid> AgentRoleIds { get; set; } = [];
}

/// <summary>
/// 查询智能体事件的请求。
/// Request used to page agent events.
/// </summary>
public class GetAgentEventsRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>项目内角色关联标识。 / Project-scoped role membership identifier.</summary>
    public Guid ProjectAgentRoleId { get; set; }

    /// <summary>页码。 / Page number.</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页大小。 / Page size.</summary>
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// 向智能体发送消息的请求。
/// Request used to send a message to an agent.
/// </summary>
public class SendAgentMessageRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>项目内角色关联标识。 / Project-scoped role membership identifier.</summary>
    public Guid ProjectAgentRoleId { get; set; }

    /// <summary>消息正文。 / Message body.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 项目成员在当前运行环境下的运行时预览。
/// Runtime preview of a project agent in the current execution environment.
/// </summary>
public class AgentRuntimePreviewDto
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>项目成员标识。 / Project-agent identifier.</summary>
    public Guid ProjectAgentRoleId { get; set; }

    /// <summary>角色定义标识。 / Agent-role identifier.</summary>
    public Guid AgentRoleId { get; set; }

    /// <summary>角色显示名称。 / Role display name.</summary>
    public string? RoleName { get; set; }

    /// <summary>角色职位。 / Role job title.</summary>
    public string? JobTitle { get; set; }

    /// <summary>当前环境下生成的系统提示词。 / Generated system prompt for the current environment.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>最终会请求给模型的工具列表。 / Final tool list requested for the model.</summary>
    public List<AgentRuntimeToolDto> Tools { get; set; } = [];

    /// <summary>当前环境解析出的 skill runtime。 / Skill runtime resolved for the current environment.</summary>
    public List<AgentRuntimeSkillDto> Skills { get; set; } = [];

    /// <summary>当前环境下缺失的 skill 绑定。 / Missing skill bindings in the current environment.</summary>
    public List<AgentRuntimeMissingSkillDto> MissingSkills { get; set; } = [];
}

/// <summary>
/// 运行时工具预览项。
/// Runtime tool preview item.
/// </summary>
public class AgentRuntimeToolDto
{
    /// <summary>工具名称。 / Tool name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>工具描述。 / Tool description.</summary>
    public string? Description { get; set; }

    /// <summary>工具来源，例如 builtin 或 mcp。 / Tool source such as builtin or mcp.</summary>
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// 已解析的 skill runtime 项。
/// Resolved skill runtime entry.
/// </summary>
public class AgentRuntimeSkillDto
{
    /// <summary>安装键。 / Install key.</summary>
    public string InstallKey { get; set; } = string.Empty;

    /// <summary>技能标识。 / Skill identifier.</summary>
    public string SkillId { get; set; } = string.Empty;

    /// <summary>显示名称。 / Display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>来源。 / Source.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>目录路径。 / Directory path.</summary>
    public string DirectoryPath { get; set; } = string.Empty;
}

/// <summary>
/// 缺失的 skill 绑定预览项。
/// Missing skill-binding preview item.
/// </summary>
public class AgentRuntimeMissingSkillDto
{
    /// <summary>绑定作用域。 / Binding scope.</summary>
    public string BindingScope { get; set; } = string.Empty;

    /// <summary>技能安装键。 / Skill install key.</summary>
    public string SkillInstallKey { get; set; } = string.Empty;

    /// <summary>技能标识。 / Skill identifier.</summary>
    public string SkillId { get; set; } = string.Empty;

    /// <summary>显示名称。 / Display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>缺失原因。 / Missing reason.</summary>
    public string Message { get; set; } = string.Empty;
}
