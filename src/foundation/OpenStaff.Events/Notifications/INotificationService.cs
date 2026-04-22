using OpenStaff.Entities;

namespace OpenStaff.Core.Notifications;

/// <summary>
/// 统一通知服务接口 / Unified notification service that acts as the single entry point for realtime delivery.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 向指定频道发送通知（通用） / Send a generic notification to a specific channel.
    /// </summary>
    /// <param name="channel">目标频道 / Target channel.</param>
    /// <param name="eventType">事件类型 / Event type.</param>
    /// <param name="payload">可选载荷 / Optional payload.</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    Task NotifyAsync(string channel, string eventType, object? payload = null, CancellationToken ct = default);

    /// <summary>
    /// 发布会话事件（写入 ReplaySubject + 推送到 session 频道） / Publish a session event to persistence and the session channel.
    /// </summary>
    /// <param name="sessionId">会话标识 / Session identifier.</param>
    /// <param name="sessionEvent">要发布的会话事件 / Session event to publish.</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    Task PublishSessionEventAsync(Guid sessionId, SessionEvent sessionEvent, CancellationToken ct = default);
}

/// <summary>
/// 频道命名约定 / Channel naming conventions.
/// </summary>
public static class Channels
{
    /// <summary>全局广播频道 / Global broadcast channel.</summary>
    public const string Global = "global";

    /// <summary>获取项目频道名 / Build the channel name for a project.</summary>
    /// <param name="projectId">项目标识 / Project identifier.</param>
    /// <returns>项目频道名 / Project channel name.</returns>
    public static string Project(Guid projectId) => $"project:{projectId}";

    /// <summary>获取会话频道名 / Build the channel name for a session.</summary>
    /// <param name="sessionId">会话标识 / Session identifier.</param>
    /// <returns>会话频道名 / Session channel name.</returns>
    public static string Session(Guid sessionId) => $"session:{sessionId}";

    /// <summary>获取任务频道名 / Build the channel name for a task.</summary>
    /// <param name="taskId">任务标识 / Task identifier.</param>
    /// <returns>任务频道名 / Task channel name.</returns>
    public static string Task(Guid taskId) => $"task:{taskId}";

    /// <summary>从频道名解析 ID / Parse the trailing identifier from a channel name.</summary>
    /// <param name="channel">频道名 / Channel name.</param>
    /// <returns>解析出的标识；不匹配时返回 <c>null</c> / Parsed identifier, or <c>null</c> when the channel does not match the expected pattern.</returns>
    public static Guid? ParseId(string channel)
    {
        var idx = channel.IndexOf(':');
        if (idx < 0) return null;

        // zh-CN: 仅解析第一个冒号后的内容，保持 project:{id} / session:{id} 这类约定简单稳定。
        // en: Only parse the suffix after the first colon to keep conventions like project:{id} and session:{id} simple and stable.
        return Guid.TryParse(channel.AsSpan(idx + 1), out var id) ? id : null;
    }
}

/// <summary>
/// 统一通知载荷格式 / Common envelope for notification messages.
/// </summary>
public class NotificationMessage
{
    /// <summary>目标频道 / Target channel.</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>事件类型 / Event type.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>事件载荷 / Event payload.</summary>
    public object? Payload { get; set; }

    /// <summary>发送时间（UTC） / Timestamp in UTC.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>可选顺序号 / Optional sequence number.</summary>
    public long? SequenceNo { get; set; }
}
