namespace OpenStaff.Dtos;

/// <summary>
/// 通用消息响应。
/// Generic message response payload.
/// </summary>
public class ApiMessageDto
{
    /// <summary>消息文本。 / Message text.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 通用状态响应。
/// Generic status response payload.
/// </summary>
public class ApiStatusDto
{
    /// <summary>状态值。 / Status value.</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// 通用文本内容响应。
/// Generic text content response payload.
/// </summary>
public class ContentDto
{
    /// <summary>文本内容。 / Text content.</summary>
    public string? Content { get; set; }
}

/// <summary>
/// 通用差异内容响应。
/// Generic diff response payload.
/// </summary>
public class DiffDto
{
    /// <summary>差异文本。 / Diff text.</summary>
    public string? Diff { get; set; }
}

/// <summary>
/// 会话引用响应。
/// Session reference response payload.
/// </summary>
public class SessionIdDto
{
    /// <summary>会话标识。 / Session identifier.</summary>
    public Guid SessionId { get; set; }
}

/// <summary>
/// 统一对话任务引用响应。
/// Unified response that identifies a single conversation task turn.
/// </summary>
public class ConversationTaskOutput
{
    /// <summary>本轮对话任务标识。 / Identifier of the current conversation task turn.</summary>
    public Guid TaskId { get; set; }

    /// <summary>当前任务状态。 / Current task status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>所属长期会话标识；测试场景可为空。 / Owning long-lived session identifier; can be empty for test chat.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>所属项目标识；无项目上下文时可为空。 / Owning project identifier; empty when no project context exists.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>会话场景名称。 / Session scene name.</summary>
    public string? Scene { get; set; }

    /// <summary>统一入口类型。 / Stable business entry kind.</summary>
    public string? EntryKind { get; set; }

    /// <summary>目标项目成员标识；仅项目私聊等入口会返回。 / Target project-agent identifier for private-entry style flows.</summary>
    public Guid? ProjectAgentRoleId { get; set; }

    /// <summary>显式角色定义标识；仅测试对话等入口会返回。 / Explicit agent-role identifier for role-driven flows such as test chat.</summary>
    public Guid? AgentRoleId { get; set; }

    /// <summary>当前任务是否已进入等待用户补充输入。 / Whether the current task is waiting for more user input.</summary>
    public bool IsAwaitingInput { get; set; }
}

/// <summary>
/// 批量更新结果响应。
/// Batch update result payload.
/// </summary>
public class UpdatedCountDto
{
    /// <summary>受影响条数。 / Affected item count.</summary>
    public int Updated { get; set; }
}

/// <summary>
/// 健康检查响应。
/// Health check response payload.
/// </summary>
public class HealthStatusDto
{
    /// <summary>健康状态。 / Health status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>响应时间戳（UTC）。 / Response timestamp in UTC.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>应用版本。 / Application version.</summary>
    public string Version { get; set; } = string.Empty;
}
