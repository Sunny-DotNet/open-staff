using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Sessions.Services;
/// <summary>
/// 会话流管理器，负责 ReplaySubject 生命周期以及会话事件持久化。
/// Session stream manager that owns ReplaySubject lifecycles and session-event persistence.
/// </summary>
public class SessionStreamManager : IDisposable
{
    private readonly ConcurrentDictionary<Guid, SessionStream> _streams = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionStreamManager> _logger;

    /// <summary>
    /// 初始化会话流管理器。
    /// Initializes the session stream manager.
    /// </summary>
    public SessionStreamManager(IServiceScopeFactory scopeFactory, ILogger<SessionStreamManager> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 创建新的内存会话流。
    /// Creates a new in-memory session stream.
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
    /// 获取活跃会话流；若不存在则返回 <see langword="null"/>。
    /// Gets the active session stream, or <see langword="null"/> when none exists.
    /// </summary>
    public SessionStream? GetActive(Guid sessionId)
    {
        return _streams.TryGetValue(sessionId, out var stream) ? stream : null;
    }

    /// <summary>
    /// 判断指定会话是否仍由内存流托管。
    /// Determines whether the specified session is still hosted by an in-memory stream.
    /// </summary>
    public bool IsActive(Guid sessionId) => _streams.ContainsKey(sessionId);

    /// <summary>
    /// 完成会话流并持久化当前缓冲事件。
    /// Completes the session stream and persists the buffered events.
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
    /// 完成临时会话流但不持久化。
    /// Completes a transient session stream without persisting it.
    /// </summary>
    public void CompleteTransient(Guid sessionId, TimeSpan? cleanupDelay = null)
    {
        if (!_streams.TryGetValue(sessionId, out var stream))
            return;

        stream.Complete();
        _logger.LogInformation("Session stream {SessionId} completed (transient, {Count} events)",
            sessionId, stream.EventCount);

        // zh-CN: 这里保留一个短暂回放窗口，避免测试会话在客户端刚订阅时就被立即清理。
        // en: Keep a short replay window so transient sessions are not removed before late subscribers can read them.
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
    /// 取消会话流，持久化已产生的事件并终止订阅。
    /// Cancels the session stream, persists produced events, and terminates subscribers.
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
    /// 订阅会话事件流；活跃会话走内存流，已完成会话回放数据库历史。
    /// Subscribes to the session event stream; active sessions use the in-memory stream, completed sessions replay database history.
    /// </summary>
    public async IAsyncEnumerable<SessionEvent> SubscribeAsync(
        Guid sessionId,
        long afterSequenceNo = 0,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var stream = GetActive(sessionId);
        if (stream != null)
        {
            // zh-CN: 活跃流通过 ReplaySubject 同时覆盖“回放历史”和“继续推送”两种需求。
            // en: The active ReplaySubject handles both “replay existing events” and “stream future events”.
            await foreach (var evt in stream.AsAsyncEnumerable(afterSequenceNo, ct))
            {
                yield return evt;
            }
        }
        else
        {
            // zh-CN: 会话已结束时改为从数据库回放，避免为了历史数据长期保留内存流。
            // en: Once a session is complete, replay from the database instead of keeping the in-memory stream alive indefinitely.
            using var persistence = CreatePersistenceScope();
            var events = await persistence.SessionEvents
                .Where(e => e.SessionId == sessionId)
                .Where(e => e.SequenceNo > afterSequenceNo)
                .OrderBy(e => e.SequenceNo)
                .ToListAsync(ct);

            foreach (var evt in events)
            {
                yield return evt;
            }
        }
    }

    /// <summary>
    /// 将内存缓冲事件批量写入数据库；空批次直接跳过，并在独立作用域中完成持久化以避免复用失效上下文。
    /// Persists buffered events to the database in a batch; empty batches are skipped, and a fresh scope is used to avoid reusing a stale DbContext.
    /// </summary>
    private async Task PersistEventsAsync(Guid sessionId, IReadOnlyList<SessionEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return;

        using var persistence = CreatePersistenceScope();
        persistence.SessionEvents.AddRange(events);
        await persistence.RepositoryContext.SaveChangesAsync(ct);

        _logger.LogDebug("Persisted {Count} events for session {SessionId}", events.Count, sessionId);
    }

    private PersistenceScope CreatePersistenceScope()
    {
        var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        return new PersistenceScope(
            scope,
            services.GetRequiredService<ISessionEventRepository>(),
            services.GetRequiredService<IRepositoryContext>());
    }

    /// <summary>
    /// 释放所有活跃流。
    /// Disposes all active streams.
    /// </summary>
    public void Dispose()
    {
        foreach (var (_, stream) in _streams)
        {
            stream.Dispose();
        }
        _streams.Clear();
    }

    private sealed class PersistenceScope : IDisposable
    {
        private readonly IServiceScope _scope;

        public PersistenceScope(
            IServiceScope scope,
            ISessionEventRepository sessionEvents,
            IRepositoryContext repositoryContext)
        {
            _scope = scope;
            SessionEvents = sessionEvents;
            RepositoryContext = repositoryContext;
        }

        public ISessionEventRepository SessionEvents { get; }

        public IRepositoryContext RepositoryContext { get; }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}

/// <summary>
/// 单个会话的流包装，封装 ReplaySubject、缓冲区和序号分配。
/// Wrapper for a single session stream that encapsulates the ReplaySubject, buffer, and sequence allocation.
/// </summary>
public class SessionStream : IDisposable
{
    private readonly ReplaySubject<SessionEvent> _subject = new();
    private readonly List<SessionEvent> _buffer = new();
    private readonly object _lock = new();
    private long _sequenceNo;

    /// <summary>
    /// 所属会话标识。
    /// Session identifier owned by this stream.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// 当前缓冲事件数量。
    /// Number of events currently buffered in memory.
    /// </summary>
    public int EventCount => _buffer.Count;

    /// <summary>
    /// 初始化单个会话流包装器。
    /// Initializes a wrapper for a single session stream.
    /// </summary>
    public SessionStream(Guid sessionId)
    {
        SessionId = sessionId;
    }

    /// <summary>
    /// 推送事件到流中。
    /// Pushes an event into the stream.
    /// </summary>
    public void Push(SessionEvent evt)
    {
        lock (_lock)
        {
            // zh-CN: 序号和 SessionId 必须在同一把锁内分配，确保回放顺序与内存缓冲保持一致。
            // en: Sequence numbers and SessionId must be assigned under the same lock so replay order matches the in-memory buffer.
            evt.SequenceNo = Interlocked.Increment(ref _sequenceNo);
            evt.SessionId = SessionId;
            _buffer.Add(evt);
        }
        _subject.OnNext(evt);
    }

    /// <summary>
    /// 创建并推送事件。
    /// Creates and pushes a new event.
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
    /// 标记流完成。
    /// Marks the stream as completed.
    /// </summary>
    public void Complete() => _subject.OnCompleted();

    /// <summary>
    /// 标记流错误。
    /// Marks the stream as faulted.
    /// </summary>
    public void Error(Exception ex) => _subject.OnError(ex);

    /// <summary>
    /// 获取已缓冲的所有事件。
    /// Gets all buffered events.
    /// </summary>
    public IReadOnlyList<SessionEvent> GetBufferedEvents()
    {
        lock (_lock)
        {
            return _buffer.ToList();
        }
    }

    /// <summary>
    /// 将流转换为 <see cref="IAsyncEnumerable{T}"/>。
    /// Converts the stream to an <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    public async IAsyncEnumerable<SessionEvent> AsAsyncEnumerable(
        long afterSequenceNo = 0,
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
                if (evt.SequenceNo <= afterSequenceNo)
                    return;

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
                // zh-CN: 完成信号到达后先排空队列，避免最后一批事件因竞争而丢失。
                // en: Drain the queue after completion so the final batch of events is not lost to timing races.
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

    /// <summary>
    /// 释放底层 ReplaySubject。
    /// Disposes the underlying ReplaySubject.
    /// </summary>
    public void Dispose()
    {
        _subject.Dispose();
    }
}

