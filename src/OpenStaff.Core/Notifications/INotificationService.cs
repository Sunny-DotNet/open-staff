using OpenStaff.Core.Models;

namespace OpenStaff.Core.Notifications;

/// <summary>
/// 统一通知服务接口 — 所有实时推送的唯一入口
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 向指定频道发送通知（通用）
    /// </summary>
    Task NotifyAsync(string channel, string eventType, object? payload = null, CancellationToken ct = default);

    /// <summary>
    /// 发布会话事件（写入 ReplaySubject + 推送到 session 频道）
    /// </summary>
    Task PublishSessionEventAsync(Guid sessionId, SessionEvent sessionEvent, CancellationToken ct = default);
}

/// <summary>
/// 频道命名约定
/// </summary>
public static class Channels
{
    public const string Global = "global";

    public static string Project(Guid projectId) => $"project:{projectId}";
    public static string Session(Guid sessionId) => $"session:{sessionId}";

    /// <summary>从频道名解析 ID</summary>
    public static Guid? ParseId(string channel)
    {
        var idx = channel.IndexOf(':');
        if (idx < 0) return null;
        return Guid.TryParse(channel.AsSpan(idx + 1), out var id) ? id : null;
    }
}

/// <summary>
/// 统一通知载荷格式
/// </summary>
public class NotificationMessage
{
    public string Channel { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public object? Payload { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public long? SequenceNo { get; set; }
}
