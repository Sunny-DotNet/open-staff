using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using OpenStaff.Api.Hubs;
using OpenStaff.Core.Models;
using OpenStaff.Core.Notifications;

namespace OpenStaff.Api.Services;

/// <summary>
/// 统一通知服务实现 — SignalR 推送 + ReplaySubject 缓冲
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly SessionStreamManager _streamManager;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        SessionStreamManager streamManager,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _streamManager = streamManager;
        _logger = logger;
    }

    /// <summary>
    /// 通用通知 — 推送到 SignalR 频道
    /// </summary>
    public async Task NotifyAsync(string channel, string eventType, object? payload = null, CancellationToken ct = default)
    {
        var message = new NotificationMessage
        {
            Channel = channel,
            EventType = eventType,
            Payload = payload,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            await _hubContext.Clients.Group(channel).SendAsync("Notify", message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to channel {Channel}: {EventType}", channel, eventType);
        }
    }

    /// <summary>
    /// 会话事件 — 写入 ReplaySubject（Streaming 消费）+ 推送 Notify 给 session 频道
    /// </summary>
    public async Task PublishSessionEventAsync(Guid sessionId, SessionEvent sessionEvent, CancellationToken ct = default)
    {
        // 1. 写入 ReplaySubject（供 Streaming API 消费）
        var stream = _streamManager.GetActive(sessionId);
        if (stream != null)
        {
            stream.Push(sessionEvent);
        }
        else
        {
            _logger.LogWarning("Session {SessionId} stream not active, event {EventType} may be lost",
                sessionId, sessionEvent.EventType);
        }

        // 2. 同时通过 Notify 推送给已加入 session 频道但未使用 Streaming 的客户端
        // （双保险：Streaming 客户端从 IAsyncEnumerable 获取，普通客户端从 Notify 获取）
        var channel = Channels.Session(sessionId);
        var message = new NotificationMessage
        {
            Channel = channel,
            EventType = sessionEvent.EventType,
            Payload = sessionEvent.Payload,
            Timestamp = sessionEvent.CreatedAt,
            SequenceNo = sessionEvent.SequenceNo
        };

        try
        {
            await _hubContext.Clients.Group(channel).SendAsync("Notify", message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push session event {EventType} for session {SessionId}",
                sessionEvent.EventType, sessionId);
        }
    }
}
