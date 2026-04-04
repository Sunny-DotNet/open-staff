using Microsoft.AspNetCore.SignalR;
using OpenStaff.Api.Services;
using OpenStaff.Core.Models;

namespace OpenStaff.Api.Hubs;

/// <summary>
/// 统一通知 Hub — 所有实时推送的唯一 SignalR 端点
/// 频道: global, project:{id}, session:{id}
/// 客户端方法: Notify (通用通知), StreamSession (会话流 Streaming)
/// </summary>
public class NotificationHub : Hub
{
    private readonly SessionStreamManager _streamManager;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(SessionStreamManager streamManager, ILogger<NotificationHub> logger)
    {
        _streamManager = streamManager;
        _logger = logger;
    }

    /// <summary>加入频道</summary>
    public async Task JoinChannel(string channel)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, channel);
        _logger.LogDebug("Client {ConnectionId} joined channel {Channel}", Context.ConnectionId, channel);
    }

    /// <summary>离开频道</summary>
    public async Task LeaveChannel(string channel)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);
        _logger.LogDebug("Client {ConnectionId} left channel {Channel}", Context.ConnectionId, channel);
    }

    /// <summary>
    /// 会话事件 Streaming — 返回 IAsyncEnumerable，适合高频推送
    /// 客户端: connection.stream("StreamSession", sessionId)
    /// </summary>
    public async IAsyncEnumerable<SessionEvent> StreamSession(
        Guid sessionId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Client {ConnectionId} streaming session {SessionId}",
            Context.ConnectionId, sessionId);

        // 自动加入 session 频道
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}", ct);

        await foreach (var evt in _streamManager.SubscribeAsync(sessionId, ct))
        {
            yield return evt;
        }

        // 流结束后自动离开频道
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session:{sessionId}");
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client {ConnectionId} disconnected", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
