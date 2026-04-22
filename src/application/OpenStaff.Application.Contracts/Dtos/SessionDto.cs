namespace OpenStaff.Dtos;

/// <summary>
/// 会话摘要信息。
/// Summary information for a chat session.
/// </summary>
public class SessionDto
{
    /// <summary>会话唯一标识。 / Unique session identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>所属项目标识。 / Owning project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>当前会话状态。 / Current session status.</summary>
    public string? Status { get; set; }

    /// <summary>会话场景名称。 / Session scene name.</summary>
    public string? Scene { get; set; }

    /// <summary>保留用于展示/审计的原始用户输入。 / Raw user input retained for display and audit.</summary>
    public string? Input { get; set; }

    /// <summary>最终结果摘要。 / Final result summary.</summary>
    public string? Result { get; set; }

    /// <summary>上下文策略键。 / Context strategy key.</summary>
    public string? ContextStrategy { get; set; }

    /// <summary>创建时间（UTC）。 / Creation time in UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>完成时间（UTC）。 / Completion time in UTC.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>是否仍有活跃流通道。 / Whether the session still has an active in-memory stream.</summary>
    public bool IsActive { get; set; }

    /// <summary>会话栈帧列表。 / Frame stack metadata for the session.</summary>
    public List<SessionFrameDto>? Frames { get; set; }
}

/// <summary>
/// 会话中的单个执行帧。
/// Single execution frame within a session stack.
/// </summary>
public class SessionFrameDto
{
    /// <summary>帧唯一标识。 / Unique frame identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>父帧标识。 / Parent frame identifier.</summary>
    public Guid? ParentFrameId { get; set; }

    /// <summary>关联任务标识。 / Associated task identifier.</summary>
    public Guid? TaskId { get; set; }

    /// <summary>所属执行包标识。 / Owning execution-package identifier.</summary>
    public Guid? ExecutionPackageId { get; set; }

    /// <summary>进入该帧的首条消息标识。 / Identifier of the entry message for this frame.</summary>
    public Guid? EntryMessageId { get; set; }

    /// <summary>触发该帧的父消息标识。 / Parent message identifier that spawned this frame.</summary>
    public Guid? ParentMessageId { get; set; }

    /// <summary>执行该帧的角色标识。 / Role identifier that executed this frame.</summary>
    public Guid? AgentRoleId { get; set; }

    /// <summary>执行该帧的项目内角色关联标识。 / Project-scoped role membership identifier that executed this frame.</summary>
    public Guid? ProjectAgentRoleId { get; set; }

    /// <summary>帧目标角色标识。 / Target role identifier of the frame.</summary>
    public Guid? TargetAgentRoleId { get; set; }

    /// <summary>帧目标项目内角色关联标识。 / Target project-scoped role membership identifier of the frame.</summary>
    public Guid? TargetProjectAgentRoleId { get; set; }

    /// <summary>发起该帧的角色标识。 / Role identifier that initiated the frame.</summary>
    public Guid? InitiatorAgentRoleId { get; set; }

    /// <summary>发起该帧的项目内角色关联标识。 / Project-scoped role membership identifier that initiated the frame.</summary>
    public Guid? InitiatorProjectAgentRoleId { get; set; }

    /// <summary>帧处理目的。 / Purpose handled by the frame.</summary>
    public string? Purpose { get; set; }

    /// <summary>帧状态。 / Frame status.</summary>
    public string? Status { get; set; }

    /// <summary>帧执行结果。 / Frame execution result.</summary>
    public string? Result { get; set; }

    /// <summary>栈深度，根帧通常为 0。 / Stack depth, where the root frame is typically 0.</summary>
    public int Depth { get; set; }

    /// <summary>用于展示的排序值。 / Ordering value used for presentation.</summary>
    public int Order { get; set; }

    /// <summary>创建时间（UTC）。 / Creation time in UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>完成时间（UTC）。 / Completion time in UTC.</summary>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// 会话事件投影。
/// Projected session event.
/// </summary>
public class SessionEventDto
{
    /// <summary>事件唯一标识。 / Unique event identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>所属会话标识。 / Owning session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>关联帧标识。 / Associated frame identifier.</summary>
    public Guid? FrameId { get; set; }

    /// <summary>关联消息标识。 / Associated message identifier.</summary>
    public Guid? MessageId { get; set; }

    /// <summary>关联执行包标识。 / Associated execution-package identifier.</summary>
    public Guid? ExecutionPackageId { get; set; }

    /// <summary>来源帧标识。 / Source frame identifier.</summary>
    public Guid? SourceFrameId { get; set; }

    /// <summary>来源 effect 序号。 / Source effect index.</summary>
    public int? SourceEffectIndex { get; set; }

    /// <summary>事件类型。 / Event type.</summary>
    public string? EventType { get; set; }

    /// <summary>序列化事件负载。 / Serialized event payload.</summary>
    public string? Payload { get; set; }

    /// <summary>会话内单调递增序号。 / Monotonically increasing sequence number within the session.</summary>
    public long SequenceNo { get; set; }

    /// <summary>创建时间（UTC）。 / Creation time in UTC.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 会话消息投影。
/// Projected chat message within a session.
/// </summary>
public class ChatMessageDto
{
    /// <summary>消息唯一标识。 / Unique message identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>所属会话标识。 / Owning session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>所属帧标识。 / Owning frame identifier.</summary>
    public Guid FrameId { get; set; }

    /// <summary>所属执行包标识。 / Owning execution-package identifier.</summary>
    public Guid? ExecutionPackageId { get; set; }

    /// <summary>来源帧标识。 / Originating frame identifier.</summary>
    public Guid? OriginatingFrameId { get; set; }

    /// <summary>父消息标识。 / Parent message identifier.</summary>
    public Guid? ParentMessageId { get; set; }

    /// <summary>消息角色，例如 user 或 assistant。 / Message role such as user or assistant.</summary>
    public string? Role { get; set; }

    /// <summary>生成该消息的角色标识。 / Agent-role identifier that produced the message.</summary>
    public Guid? AgentRoleId { get; set; }

    /// <summary>生成该消息的项目内角色关联标识。 / Project-scoped role membership identifier that produced the message.</summary>
    public Guid? ProjectAgentRoleId { get; set; }

    /// <summary>消息正文。 / Message body.</summary>
    public string? Content { get; set; }

    /// <summary>消息内容类型。 / Message content type.</summary>
    public string? ContentType { get; set; }

    /// <summary>兼容旧投影的总 Token 数。 / Total token count kept for backward-compatible projections.</summary>
    public int? TokenUsage { get; set; }

    /// <summary>总耗时（毫秒）。 / Total duration in milliseconds.</summary>
    public long? DurationMs { get; set; }

    /// <summary>细化的 Token 使用统计。 / Detailed token usage statistics.</summary>
    public ChatMessageUsageDto? Usage { get; set; }

    /// <summary>细化的时序统计。 / Detailed timing statistics.</summary>
    public ChatMessageTimingDto? Timing { get; set; }

    /// <summary>创建时间（UTC）。 / Creation time in UTC.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 消息 Token 使用统计。
/// Token usage statistics for a chat message.
/// </summary>
public class ChatMessageUsageDto
{
    /// <summary>输入 Token 数。 / Input token count.</summary>
    public int? InputTokens { get; set; }

    /// <summary>输出 Token 数。 / Output token count.</summary>
    public int? OutputTokens { get; set; }

    /// <summary>总 Token 数。 / Total token count.</summary>
    public int? TotalTokens { get; set; }
}

/// <summary>
/// 消息耗时统计。
/// Timing statistics for a chat message.
/// </summary>
public class ChatMessageTimingDto
{
    /// <summary>总耗时（毫秒）。 / Total time in milliseconds.</summary>
    public long? TotalMs { get; set; }

    /// <summary>首 Token 延迟（毫秒）。 / First-token latency in milliseconds.</summary>
    public long? FirstTokenMs { get; set; }
}

/// <summary>
/// 创建会话的输入参数。
/// Input used to create a session.
/// </summary>
public class CreateSessionInput
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>用户输入内容。 / User input content.</summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>展示/审计用原始输入；缺省时回退到 <see cref="Input"/>。 / Raw input for display and audit; falls back to <see cref="Input"/> when omitted.</summary>
    public string? RawInput { get; set; }

    /// <summary>上下文策略键。 / Context strategy key.</summary>
    public string? ContextStrategy { get; set; }

    /// <summary>会话场景名称。 / Session scene name.</summary>
    public string? Scene { get; set; }

    /// <summary>结构化提及信息。 / Structured mention metadata.</summary>
    public List<ConversationMentionDto>? Mentions { get; set; }
}

/// <summary>
/// 创建会话的结果。
/// Result returned after creating a session.
/// </summary>
public class CreateSessionOutput
{
    /// <summary>新建或复用的会话标识。 / Identifier of the created or reused session.</summary>
    public Guid SessionId { get; set; }

    /// <summary>会话当前状态。 / Current session status.</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// 发送消息后的结果。
/// Result returned after sending a message.
/// </summary>
public class SendMessageOutput
{
    /// <summary>消息处理状态。 / Message processing status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>会话是否正等待用户补充输入。 / Whether the session is currently waiting for additional user input.</summary>
    public bool IsAwaitingInput { get; set; }
}

/// <summary>
/// 聊天消息分页结果。
/// Paginated message payload for chat history.
/// </summary>
public class ChatMessageListOutput
{
    /// <summary>当前页消息。 / Messages returned for the current page.</summary>
    public List<ChatMessageDto> Messages { get; set; } = [];

    /// <summary>总消息数。 / Total number of visible messages.</summary>
    public int Total { get; set; }
}

/// <summary>
/// 向会话发送消息的请求。
/// Request used to send a message to a session.
/// </summary>
public class SendSessionMessageRequest
{
    /// <summary>会话标识。 / Session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>用户输入内容。 / User input content.</summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>展示/审计用原始输入；缺省时回退到 <see cref="Input"/>。 / Raw input for display and audit; falls back to <see cref="Input"/> when omitted.</summary>
    public string? RawInput { get; set; }

    /// <summary>结构化提及信息。 / Structured mention metadata.</summary>
    public List<ConversationMentionDto>? Mentions { get; set; }
}

/// <summary>
/// 对话提及信息。
/// Structured mention metadata used by project-group chat.
/// </summary>
public class ConversationMentionDto
{
    /// <summary>原始提及文本，例如 @Monica。 / Original mention text such as @Monica.</summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>解析后的提及类型。 / Resolved mention kind.</summary>
    public string? ResolvedKind { get; set; }

    /// <summary>内置角色标识。 / Builtin role identifier.</summary>
    public string? BuiltinRole { get; set; }

    /// <summary>项目成员标识。 / Project agent role identifier.</summary>
    public Guid? ProjectAgentRoleId { get; set; }
}

/// <summary>
/// 分页获取聊天消息的请求。
/// Request used to page through chat messages.
/// </summary>
public class GetChatMessagesRequest
{
    /// <summary>会话标识。 / Session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>跳过的记录数。 / Number of messages to skip.</summary>
    public int Skip { get; set; } = 0;

    /// <summary>最多返回的记录数。 / Maximum number of messages to return.</summary>
    public int Take { get; set; } = 50;
}

/// <summary>
/// 按项目查询会话列表的请求。
/// Request used to query sessions by project.
/// </summary>
public class GetSessionsByProjectRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>返回的最大会话数。 / Maximum number of sessions to return.</summary>
    public int Limit { get; set; } = 20;

    /// <summary>可选的场景过滤条件。 / Optional scene filter.</summary>
    public string? Scene { get; set; }
}

/// <summary>
/// 查询项目活跃会话的请求。
/// Request used to query the active session for a project scene.
/// </summary>
public class GetActiveProjectSessionRequest
{
    /// <summary>项目标识。 / Project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>场景名称。 / Scene name.</summary>
    public string Scene { get; set; } = string.Empty;
}

/// <summary>
/// 查询帧消息的请求。
/// Request used to retrieve messages from a frame.
/// </summary>
public class GetFrameMessagesRequest
{
    /// <summary>会话标识。 / Session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>帧标识。 / Frame identifier.</summary>
    public Guid FrameId { get; set; }
}
