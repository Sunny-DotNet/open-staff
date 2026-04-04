using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Api.Services;

/// <summary>
/// 会话流管理器 — 管理 ReplaySubject 生命周期
/// 热会话在内存中通过 ReplaySubject 回放+推送，完成后持久化到 DB 并释放
/// </summary>
public class SessionStreamManager : IDisposable
{
    private readonly ConcurrentDictionary<Guid, SessionStream> _streams = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionStreamManager> _logger;

    public SessionStreamManager(IServiceProvider serviceProvider, ILogger<SessionStreamManager> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 创建新的会话流（内存中的 ReplaySubject）
    /// </summary>
    public SessionStream Create(Guid sessionId)
    {
        var stream = new SessionStream(sessionId);
        if (!_streams.TryAdd(sessionId, stream))
        {
            stream.Dispose();
            throw new InvalidOperationException($"Session stream {sessionId} already exists");
        }

        _logger.LogInformation("Created session stream {SessionId}", sessionId);
        return stream;
    }

    /// <summary>
    /// 获取活跃的会话流（不存在则返回 null）
    /// </summary>
    public SessionStream? GetActive(Guid sessionId)
    {
        return _streams.TryGetValue(sessionId, out var stream) ? stream : null;
    }

    /// <summary>
    /// 会话流是否活跃
    /// </summary>
    public bool IsActive(Guid sessionId) => _streams.ContainsKey(sessionId);

    /// <summary>
    /// 完成会话 — 持久化所有事件到数据库，然后释放 ReplaySubject
    /// </summary>
    public async Task CompleteAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!_streams.TryRemove(sessionId, out var stream))
            return;

        try
        {
            await PersistEventsAsync(sessionId, stream.GetBufferedEvents(), ct);
            stream.Complete();
            _logger.LogInformation("Session stream {SessionId} completed and persisted ({Count} events)",
                sessionId, stream.EventCount);
        }
        finally
        {
            stream.Dispose();
        }
    }

    /// <summary>
    /// 完成会话流但不持久化（用于测试对话等临时场景）
    /// 流保留在内存中供迟到的订阅者回放，延迟后自动清理
    /// </summary>
    public void CompleteTransient(Guid sessionId, TimeSpan? cleanupDelay = null)
    {
        if (!_streams.TryGetValue(sessionId, out var stream))
            return;

        stream.Complete();
        _logger.LogInformation("Session stream {SessionId} completed (transient, {Count} events)",
            sessionId, stream.EventCount);

        // 延迟清理，给客户端足够时间订阅和回放
        var delay = cleanupDelay ?? TimeSpan.FromSeconds(30);
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);
            if (_streams.TryRemove(sessionId, out var s))
            {
                s.Dispose();
                _logger.LogDebug("Transient session stream {SessionId} cleaned up", sessionId);
            }
        });
    }

    /// <summary>
    /// 取消会话 — 持久化已有事件，发送错误信号，释放
    /// </summary>
    public async Task CancelAsync(Guid sessionId, string reason = "Cancelled by user", CancellationToken ct = default)
    {
        if (!_streams.TryRemove(sessionId, out var stream))
            return;

        try
        {
            await PersistEventsAsync(sessionId, stream.GetBufferedEvents(), ct);
            stream.Error(new OperationCanceledException(reason));
            _logger.LogInformation("Session stream {SessionId} cancelled: {Reason}", sessionId, reason);
        }
        finally
        {
            stream.Dispose();
        }
    }

    /// <summary>
    /// 订阅会话流。如果会话活跃则订阅 ReplaySubject（自动回放），
    /// 如果不活跃则从数据库加载历史事件。
    /// </summary>
    public async IAsyncEnumerable<SessionEvent> SubscribeAsync(
        Guid sessionId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var stream = GetActive(sessionId);
        if (stream != null)
        {
            // 活跃会话 — 通过 ReplaySubject 订阅（自动回放已有事件 + 后续推送）
            await foreach (var evt in stream.AsAsyncEnumerable(ct))
            {
                yield return evt;
            }
        }
        else
        {
            // 已完成会话 — 从数据库读取
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var events = await db.SessionEvents
                .Where(e => e.SessionId == sessionId)
                .OrderBy(e => e.SequenceNo)
                .ToListAsync(ct);

            foreach (var evt in events)
            {
                yield return evt;
            }
        }
    }

    private async Task PersistEventsAsync(Guid sessionId, IReadOnlyList<SessionEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.SessionEvents.AddRange(events);
        await db.SaveChangesAsync(ct);

        _logger.LogDebug("Persisted {Count} events for session {SessionId}", events.Count, sessionId);
    }

    public void Dispose()
    {
        foreach (var (_, stream) in _streams)
        {
            stream.Dispose();
        }
        _streams.Clear();
    }
}

/// <summary>
/// 单个会话的流 — 封装 ReplaySubject
/// </summary>
public class SessionStream : IDisposable
{
    private readonly ReplaySubject<SessionEvent> _subject = new();
    private readonly List<SessionEvent> _buffer = new();
    private readonly object _lock = new();
    private long _sequenceNo;

    public Guid SessionId { get; }
    public int EventCount => _buffer.Count;

    public SessionStream(Guid sessionId)
    {
        SessionId = sessionId;
    }

    /// <summary>
    /// 推送事件到流中
    /// </summary>
    public void Push(SessionEvent evt)
    {
        lock (_lock)
        {
            evt.SequenceNo = Interlocked.Increment(ref _sequenceNo);
            evt.SessionId = SessionId;
            _buffer.Add(evt);
        }
        _subject.OnNext(evt);
    }

    /// <summary>
    /// 创建并推送事件
    /// </summary>
    public SessionEvent Push(string eventType, Guid? frameId = null, string? payload = null)
    {
        var evt = new SessionEvent
        {
            EventType = eventType,
            FrameId = frameId,
            Payload = payload,
            CreatedAt = DateTime.UtcNow
        };
        Push(evt);
        return evt;
    }

    /// <summary>
    /// 标记流完成
    /// </summary>
    public void Complete() => _subject.OnCompleted();

    /// <summary>
    /// 标记流错误
    /// </summary>
    public void Error(Exception ex) => _subject.OnError(ex);

    /// <summary>
    /// 获取已缓冲的所有事件（用于持久化）
    /// </summary>
    public IReadOnlyList<SessionEvent> GetBufferedEvents()
    {
        lock (_lock)
        {
            return _buffer.ToList();
        }
    }

    /// <summary>
    /// 转为 IAsyncEnumerable（通过 Rx 的 ToAsyncEnumerable）
    /// </summary>
    public async IAsyncEnumerable<SessionEvent> AsAsyncEnumerable(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource();
        using var reg = ct.Register(() => tcs.TrySetCanceled());
        var queue = new System.Collections.Concurrent.ConcurrentQueue<SessionEvent>();
        var signal = new SemaphoreSlim(0);
        var completed = false;
        Exception? error = null;

        using var subscription = _subject.Subscribe(
            onNext: evt =>
            {
                queue.Enqueue(evt);
                signal.Release();
            },
            onError: ex =>
            {
                error = ex;
                completed = true;
                signal.Release();
            },
            onCompleted: () =>
            {
                completed = true;
                signal.Release();
            });

        while (!ct.IsCancellationRequested)
        {
            await signal.WaitAsync(ct);

            while (queue.TryDequeue(out var evt))
            {
                yield return evt;
            }

            if (completed)
            {
                // 排空剩余
                while (queue.TryDequeue(out var evt))
                {
                    yield return evt;
                }
                if (error != null && error is not OperationCanceledException)
                    throw error;
                yield break;
            }
        }
    }

    public void Dispose()
    {
        _subject.Dispose();
    }
}
