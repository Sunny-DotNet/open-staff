
using Microsoft.AspNetCore.SignalR;
using OpenStaff.Application.Sessions.Services;
using OpenStaff.Entities;

namespace OpenStaff.HttpApi.Host.Hubs;

/// <summary>
/// 统一通知 Hub，是所有实时推送的唯一 SignalR 端点。
/// Unified notification hub that serves as the single SignalR endpoint for real-time updates.
/// </summary>
public class NotificationHub : Hub
{
    private readonly SessionStreamManager _streamManager;
    private readonly TaskStreamManager _taskStreamManager;
    private readonly ILogger<NotificationHub> _logger;

    /// <summary>
    /// 初始化统一通知 Hub。
    /// Initializes the unified notification hub.
    /// </summary>
    public NotificationHub(
        SessionStreamManager streamManager,
        TaskStreamManager taskStreamManager,
        ILogger<NotificationHub> logger)
    {
        _streamManager = streamManager;
        _taskStreamManager = taskStreamManager;
        _logger = logger;
    }

    /// <summary>
    /// 加入命名通知频道。
    /// Joins a named notification channel.
    /// </summary>
    public async Task JoinChannel(string channel)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, channel);
        _logger.LogDebug("Client {ConnectionId} joined channel {Channel}", Context.ConnectionId, channel);
    }

    /// <summary>
    /// 离开命名通知频道。
    /// Leaves a named notification channel.
    /// </summary>
    public async Task LeaveChannel(string channel)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);
        _logger.LogDebug("Client {ConnectionId} left channel {Channel}", Context.ConnectionId, channel);
    }

    /// <summary>
    /// 以流式方式订阅会话事件。
    /// Streams session events to the caller.
    /// </summary>
    public async IAsyncEnumerable<SessionEvent> StreamSession(
        Guid sessionId,
        long afterSequenceNo = 0,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Client {ConnectionId} streaming session {SessionId}", Context.ConnectionId, sessionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}", ct);

        await foreach (var evt in _streamManager.SubscribeAsync(sessionId, afterSequenceNo, ct))
        {
            yield return evt;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session:{sessionId}");
    }

    /// <summary>
    /// 以流式方式订阅单轮对话任务事件。
    /// Streams events that belong to a single conversation task turn.
    /// </summary>
    public async IAsyncEnumerable<SessionEvent> StreamTask(
        Guid taskId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Client {ConnectionId} streaming task {TaskId}", Context.ConnectionId, taskId);

        await Groups.AddToGroupAsync(Context.ConnectionId, $"task:{taskId}", ct);

        await foreach (var evt in _taskStreamManager.SubscribeAsync(taskId, ct))
        {
            yield return evt;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"task:{taskId}");
    }

    /// <summary>
    /// 处理连接断开事件。
    /// Handles connection disconnection.
    /// </summary>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client {ConnectionId} disconnected", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

