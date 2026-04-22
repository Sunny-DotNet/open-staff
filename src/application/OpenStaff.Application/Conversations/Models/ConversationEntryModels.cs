using OpenStaff.Dtos;
using OpenStaff.Entities;

namespace OpenStaff.Application.Conversations.Models;

/// <summary>
/// zh-CN: 统一对话入口的类型标识。
/// 这里单独抽一层，不是为了“枚举好看”，而是为了把测试对话、项目脑暴、项目群聊、项目私聊这四条入口先收敛成稳定语义，
/// 后面再扩运行时和执行包时，就不会继续让上层 API 直接拼底层参数。
/// 例如：
/// - TestChat -> 允许临时 override
/// - ProjectBrainstorm -> 固定秘书入口
/// - ProjectGroup -> 固定先回秘书再编排
/// - ProjectAgentPrivate -> 显式指定项目成员
/// en: Identifies the unified conversation entry type.
/// </summary>
public static class ConversationEntryKinds
{
    public const string TestChat = nameof(TestChat);
    public const string ProjectBrainstorm = nameof(ProjectBrainstorm);
    public const string ProjectGroup = nameof(ProjectGroup);
    public const string ProjectAgentPrivate = nameof(ProjectAgentPrivate);
    public const string SessionReply = nameof(SessionReply);
}

/// <summary>
/// zh-CN: 统一附件输入模型。
/// 第一版先把结构固定下来，即使当前多数入口还只传纯文本，也要先把“文本以外的输入材料”放到同一轨道里，
/// 否则后面每条入口都会各自长出一套附件字段。
/// en: Unified attachment contract shared by conversation entries.
/// </summary>
public sealed record ConversationAttachment(
    string Kind,
    string Value,
    string? Name = null,
    string? MimeType = null,
    long? Size = null);

/// <summary>
/// zh-CN: 统一提及输入模型。
/// 这里保留 rawText 和 unresolved 信息，是为了让后端后续即使没解析成功，也还能知道“用户原本提到了谁”，
/// 避免把编排线索在入口层直接丢掉。
/// en: Unified mention contract shared by project-group entries.
/// </summary>
public sealed record ConversationMention(
    string RawText,
    string? ResolvedKind = null,
    string? BuiltinRole = null,
    Guid? ProjectAgentRoleId = null);

/// <summary>
/// zh-CN: 统一对话入口基类。
/// 这一层只保留“业务入口意图”，不混入 FrameId / TaskId / ParentMessageId 这类运行时恢复细节，
/// 因为这些细节应该由统一入口服务根据场景自行补齐，而不是让上层 API 到处手写。
/// en: Base record for unified conversation entry contracts.
/// </summary>
public abstract record ConversationEntry(
    string EntryKind,
    string Input,
    IReadOnlyList<ConversationAttachment>? Attachments = null)
{
    public IReadOnlyList<ConversationAttachment> Attachments { get; init; } = Attachments ?? [];

    /// <summary>
    /// zh-CN: 入口层可额外保留一份展示/审计用原始文本，避免执行清洗覆盖用户真正输入的内容。
    /// en: Optionally retains raw display/audit text so execution cleanup does not overwrite what the user actually typed.
    /// </summary>
    public string? RawInput { get; init; }

    /// <summary>
    /// zh-CN: 返回应写入聊天历史和审计轨迹的可见文本；未提供 RawInput 时回退到执行输入。
    /// en: Returns the visible text for chat history and audit trails, falling back to the execution input when raw input is absent.
    /// </summary>
    public string DisplayInput => string.IsNullOrWhiteSpace(RawInput) ? Input : RawInput;
}

/// <summary>
/// zh-CN: 角色测试对话入口。
/// 这是唯一允许带 override 的入口，因为它本质上是“临时试跑”，而不是正式项目链路。
/// en: Entry contract for role test-chat.
/// </summary>
public sealed record TestChatEntry(
    Guid AgentRoleId,
    string Input,
    IReadOnlyList<ConversationAttachment>? Attachments = null,
    AgentRoleInput? Override = null)
    : ConversationEntry(ConversationEntryKinds.TestChat, Input, Attachments);

/// <summary>
/// zh-CN: 项目头脑风暴入口。
/// 这里不暴露 TargetRole，是因为这个入口按设计就等价于“项目里的秘书私聊”，目标角色应由服务端固定。
/// en: Entry contract for project brainstorming.
/// </summary>
public sealed record ProjectBrainstormEntry(
    Guid ProjectId,
    string Input,
    string ContextStrategy = ContextStrategies.Full,
    IReadOnlyList<ConversationAttachment>? Attachments = null)
    : ConversationEntry(ConversationEntryKinds.ProjectBrainstorm, Input, Attachments);

/// <summary>
/// zh-CN: 项目群聊入口。
/// mentions 只是“编排线索”，不是最终执行目标；真正交给谁执行，仍应由后端按项目上下文解析。
/// en: Entry contract for project group chat.
/// </summary>
public sealed record ProjectGroupEntry(
    Guid ProjectId,
    string Input,
    string ContextStrategy = ContextStrategies.Full,
    IReadOnlyList<ConversationMention>? Mentions = null,
    IReadOnlyList<ConversationAttachment>? Attachments = null)
    : ConversationEntry(ConversationEntryKinds.ProjectGroup, Input, Attachments)
{
    public IReadOnlyList<ConversationMention> Mentions { get; init; } = Mentions ?? [];
}

/// <summary>
/// zh-CN: 项目成员私聊入口。
/// 这里显式要求 projectId + projectAgentId，是为了把“项目作用域”和“最终执行者”一次性说清楚，
/// 避免后端还要猜当前 agentId 属于哪个项目。
/// en: Entry contract for project-agent private chat.
/// </summary>
public sealed record ProjectAgentPrivateEntry(
    Guid ProjectId,
    Guid ProjectAgentRoleId,
    string Input,
    IReadOnlyList<ConversationAttachment>? Attachments = null)
    : ConversationEntry(ConversationEntryKinds.ProjectAgentPrivate, Input, Attachments);

/// <summary>
/// zh-CN: 会话续写入口。
/// 这层故意不带 scene，因为正式场景的 scene 已经由 session 固化；续写只需要说“向哪条线程继续发什么”。
/// en: Entry contract for sending a follow-up message into an existing session.
/// </summary>
public sealed record SessionReplyEntry(
    Guid SessionId,
    string Input,
    IReadOnlyList<ConversationMention>? Mentions = null,
    IReadOnlyList<ConversationAttachment>? Attachments = null)
    : ConversationEntry(ConversationEntryKinds.SessionReply, Input, Attachments)
{
    public IReadOnlyList<ConversationMention> Mentions { get; init; } = Mentions ?? [];
}
