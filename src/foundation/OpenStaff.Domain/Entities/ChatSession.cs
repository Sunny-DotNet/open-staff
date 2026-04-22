using OpenStaff.Core.Agents;

namespace OpenStaff.Entities;

/// <summary>
/// 对话会话 — 用户发起的一次完整交互
/// </summary>
public class ChatSession:EntityBase<Guid>
{
    /// <summary>所属项目标识 / Owning project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>会话状态</summary>
    public string Status { get; set; } = SessionStatus.Active;

    /// <summary>用户的原始输入</summary>
    public string InitialInput { get; set; } = string.Empty;

    /// <summary>最终结果摘要</summary>
    public string? FinalResult { get; set; }

    /// <summary>上下文传递策略</summary>
    public string ContextStrategy { get; set; } = ContextStrategies.Full;

    /// <summary>会话场景</summary>
    public string Scene { get; set; } = SessionSceneTypes.ProjectBrainstorm;

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>完成时间（UTC） / Completion timestamp in UTC.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>所属项目 / Owning project.</summary>
    public Project? Project { get; set; }

    /// <summary>会话帧集合 / Frames within the session.</summary>
    public ICollection<ChatFrame> Frames { get; set; } = new List<ChatFrame>();

    /// <summary>会话事件集合 / Session events persisted for replay.</summary>
    public ICollection<SessionEvent> Events { get; set; } = new List<SessionEvent>();
}

/// <summary>
/// 会话状态常量 / Well-known chat session states.
/// </summary>
public static class SessionStatus
{
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Error = "error";
    /// <summary>等待用户输入（暂停链式流转）</summary>
    public const string AwaitingInput = "awaiting_input";
}

/// <summary>
/// 上下文传递策略常量 / Context propagation strategies between frames.
/// </summary>
public static class ContextStrategies
{
    /// <summary>完整传递所有父 Frame 消息</summary>
    public const string Full = "full";
    /// <summary>只传父 Frame 摘要</summary>
    public const string Summary = "summary";
    /// <summary>当前帧完整 + 祖先帧摘要</summary>
    public const string Hybrid = "hybrid";
}

/// <summary>
/// 会话场景常量 / Session scene type constants.
/// </summary>
public static class SessionSceneTypes
{
    public const string Test = nameof(SceneType.Test);
    public const string TeamGroup = nameof(SceneType.TeamGroup);
    public const string ProjectBrainstorm = nameof(SceneType.ProjectBrainstorm);
    public const string ProjectGroup = nameof(SceneType.ProjectGroup);
    public const string Private = nameof(SceneType.Private);

    /// <summary>
    /// 尝试将场景字符串解析为枚举值 / Try to parse a scene string into <see cref="SceneType"/> using case-insensitive enum matching.
    /// </summary>
    /// <param name="scene">场景字符串 / Scene string.</param>
    /// <param name="parsed">解析后的枚举值 / Parsed enum value.</param>
    /// <returns>解析成功时为 <c>true</c> / <c>true</c> when parsing succeeds.</returns>
    public static bool TryParse(string? scene, out SceneType parsed) =>
        Enum.TryParse(scene, ignoreCase: true, out parsed);
}
