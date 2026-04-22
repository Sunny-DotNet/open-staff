namespace OpenStaff.Agent.Services;

/// <summary>
/// zh-CN: 标识一条消息执行所处的运行场景。
/// en: Identifies the runtime scene that owns a message execution.
/// </summary>
public enum MessageScene
{
    /// <summary>
    /// zh-CN: 单体测试或调试场景。
    /// en: Test or diagnostic scene.
    /// </summary>
    Test,

    /// <summary>
    /// zh-CN: 一对一私聊场景。
    /// en: One-to-one private chat scene.
    /// </summary>
    Private,

    /// <summary>
    /// zh-CN: 团队群聊场景。
    /// en: Team group-chat scene.
    /// </summary>
    TeamGroup,

    /// <summary>
    /// zh-CN: 项目头脑风暴场景。
    /// en: Project-brainstorm scene.
    /// </summary>
    ProjectBrainstorm,

    /// <summary>
    /// zh-CN: 项目执行群聊场景。
    /// en: Project execution group-chat scene.
    /// </summary>
    ProjectGroup
}

/// <summary>
/// zh-CN: 携带将运行时消息重新关联到上层业务状态所需的持久化标识。
/// en: Carries the persistent identifiers needed to reconnect a runtime message to higher-level application state.
/// </summary>
/// <param name="ProjectId">
/// zh-CN: 所属项目标识。
/// en: The owning project identifier.
/// </param>
/// <param name="SessionId">
/// zh-CN: 所属会话标识。
/// en: The owning session identifier.
/// </param>
/// <param name="ParentMessageId">
/// zh-CN: 恢复上下文时使用的父消息标识。
/// en: The parent message identifier used to restore context.
/// </param>
/// <param name="FrameId">
/// zh-CN: 当前对话帧标识。
/// en: The current chat frame identifier.
/// </param>
/// <param name="ParentFrameId">
/// zh-CN: 父级对话帧标识。
/// en: The parent chat frame identifier.
/// </param>
/// <param name="TaskId">
/// zh-CN: 关联任务标识。
/// en: The associated task identifier.
/// </param>
/// <param name="ProjectAgentRoleId">
/// zh-CN: 关联的项目智能体实例标识。
/// en: The associated project-agent instance identifier.
/// </param>
/// <param name="TargetRole">
/// zh-CN: 期望执行的目标角色类型或别名。
/// en: The target role type or alias to execute.
/// </param>
/// <param name="InitiatorRole">
/// zh-CN: 发起此次调用的角色类型。
/// en: The role type that initiated the call.
/// </param>
/// <param name="Extra">
/// zh-CN: 运行时附加元数据。
/// en: Additional runtime metadata.
/// </param>
public readonly record struct MessageContext(
    Guid? ProjectId,
    Guid? SessionId,
    Guid? ParentMessageId,
    Guid? FrameId,
    Guid? ParentFrameId,
    Guid? TaskId,
    Guid? ProjectAgentRoleId,
    string? InitiatorRole,
    IReadOnlyDictionary<string, string>? Extra)
{
    public string? TargetRole { get; init; }

    public Guid? ExecutionPackageId { get; init; }

    public string? EntryKind { get; init; }

    public Guid? SourceFrameId { get; init; }

    public MessageContext(
        Guid? ProjectId,
        Guid? SessionId,
        Guid? ParentMessageId,
        Guid? FrameId,
        Guid? ParentFrameId,
        Guid? TaskId,
        Guid? ProjectAgentRoleId,
        string? TargetRole,
        string? InitiatorRole,
        IReadOnlyDictionary<string, string>? Extra)
        : this(ProjectId, SessionId, ParentMessageId, FrameId, ParentFrameId, TaskId, ProjectAgentRoleId, InitiatorRole, Extra)
    {
        this.TargetRole = TargetRole;
    }
}
