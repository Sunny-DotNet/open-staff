using OpenStaff.Entities;

namespace OpenStaff.Application.Conversations.Models;

/// <summary>
/// zh-CN: 系统触发型对话入口的幂等模式。
/// 这类入口通常不是用户直接发话，而是项目初始化、阶段切换、系统提醒等业务动作触发。
/// 因此需要把“是否允许重复落消息”显式建模，避免每个调用点各自写一套去重逻辑。
/// en: Idempotency modes for system-triggered conversation entries.
/// </summary>
public static class ConversationTriggerIdempotencyModes
{
    public const string None = "none";
    public const string SkipIfSceneHasMessages = "skip_if_scene_has_messages";
}

/// <summary>
/// zh-CN: 项目内系统触发型对话入口。
/// 这里刻意不用 Input 命名，是为了和“用户输入”区分开：该入口的职责是让业务事件触发一条会话中的首消息或系统消息，
/// 而不是伪装成用户手动输入。
/// en: Project-scoped system trigger that creates or reuses a conversation scene and persists a seeded message.
/// </summary>
public sealed record ProjectConversationTriggerEntry(
    Guid ProjectId,
    string Scene,
    string SessionSummary,
    string FramePurpose,
    string MessageContent,
    string? AuthorRole = null,
    string MessageRole = MessageRoles.Assistant,
    string MessageContentType = MessageContentTypes.Text,
    string ContextStrategy = ContextStrategies.Full,
    string IdempotencyMode = ConversationTriggerIdempotencyModes.SkipIfSceneHasMessages);

/// <summary>
/// zh-CN: 系统触发型对话入口的结果。
/// 返回会话/消息标识和是否真正创建了新内容，方便调用方后续决定是否继续广播 UI 或补充业务状态。
/// en: Result produced by a system-triggered conversation entry.
/// </summary>
public sealed record ProjectConversationTriggerResult(
    Guid SessionId,
    Guid? FrameId,
    Guid? MessageId,
    bool CreatedSession,
    bool CreatedMessage,
    bool Skipped);
