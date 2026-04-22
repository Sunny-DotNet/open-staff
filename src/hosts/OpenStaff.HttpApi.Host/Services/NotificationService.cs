
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using OpenStaff.HttpApi.Host.Hubs;
using OpenStaff.Application.Sessions.Services;
using OpenStaff.Entities;
using OpenStaff.Core.Notifications;

namespace OpenStaff.HttpApi.Host.Services;

/// <summary>
/// 统一通知服务实现，负责 SignalR 推送与会话流桥接。
/// Notification service implementation that bridges SignalR pushes and session streams.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly SessionStreamManager _streamManager;
    private readonly TaskStreamManager _taskStreamManager;
    private readonly ILogger<NotificationService> _logger;

    /// <summary>
    /// 初始化统一通知服务。
    /// Initializes the unified notification service.
    /// </summary>
    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        SessionStreamManager streamManager,
        TaskStreamManager taskStreamManager,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _streamManager = streamManager;
        _taskStreamManager = taskStreamManager;
        _logger = logger;
    }

    /// <summary>
    /// 发送通用频道通知。
    /// Sends a generic channel notification.
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
    /// 发布会话事件到内存流和频道通知。
    /// Publishes a session event to both the in-memory stream and the notification channel.
    /// </summary>
    public async Task PublishSessionEventAsync(Guid sessionId, SessionEvent sessionEvent, CancellationToken ct = default)
    {
        var stream = _streamManager.GetActive(sessionId);
        if (stream != null)
        {
            stream.Push(sessionEvent);
        }
        else
        {
            _logger.LogWarning("Session {SessionId} stream not active, event {EventType} may be lost", sessionId, sessionEvent.EventType);
        }

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
            _logger.LogError(ex, "Failed to push session event {EventType} for session {SessionId}", sessionEvent.EventType, sessionId);
        }

        if (sessionEvent.ExecutionPackageId.HasValue)
        {
            var taskId = sessionEvent.ExecutionPackageId.Value;
            _taskStreamManager.Push(taskId, sessionEvent);

            var taskChannel = Channels.Task(taskId);
            var taskMessage = new NotificationMessage
            {
                Channel = taskChannel,
                EventType = sessionEvent.EventType,
                Payload = sessionEvent.Payload,
                Timestamp = sessionEvent.CreatedAt,
                SequenceNo = sessionEvent.SequenceNo
            };

            try
            {
                await _hubContext.Clients.Group(taskChannel).SendAsync("Notify", taskMessage, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push task event {EventType} for task {TaskId}", sessionEvent.EventType, taskId);
            }
        }
    }
}

