using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Entities;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Sessions.Services;

/// <summary>
/// 任务流管理器，负责按 taskId 回放单轮对话执行事件。
/// Task stream manager that replays per-turn conversation events keyed by taskId.
/// </summary>
public sealed class TaskStreamManager : IDisposable
{
    private static readonly TimeSpan DefaultCleanupDelay = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<Guid, TaskStream> _streams = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskStreamManager> _logger;

    public TaskStreamManager(IServiceScopeFactory scopeFactory, ILogger<TaskStreamManager> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 将一条事件推入指定任务流；若任务流尚不存在则自动创建。
    /// Pushes an event into the task stream and lazily creates the stream on first use.
    /// </summary>
    public void Push(Guid taskId, SessionEvent evt)
    {
        var stream = _streams.GetOrAdd(taskId, id =>
        {
            _logger.LogDebug("Created task stream {TaskId}", id);
            return new TaskStream(id);
        });

        stream.Push(CloneEvent(evt));
        ScheduleCleanup(taskId, stream.Version, DefaultCleanupDelay);
    }

    /// <summary>
    /// 显式结束一个仅内存保留的任务流，并保留短暂回放窗口。
    /// Explicitly completes a transient task stream while keeping a short replay window.
    /// </summary>
    public void CompleteTransient(Guid taskId, TimeSpan? cleanupDelay = null)
    {
        if (!_streams.TryGetValue(taskId, out var stream))
            return;

        stream.Complete();
        ScheduleCleanup(taskId, stream.Version, cleanupDelay ?? DefaultCleanupDelay);
    }

    /// <summary>
    /// 按任务订阅事件；活跃任务读取内存流，已结束任务回退到数据库按 executionPackageId 回放。
    /// Subscribes to a task stream, using the in-memory replay when active and falling back to persisted session events otherwise.
    /// </summary>
    public async IAsyncEnumerable<SessionEvent> SubscribeAsync(
        Guid taskId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_streams.TryGetValue(taskId, out var stream))
        {
            await foreach (var evt in stream.AsAsyncEnumerable(ct))
            {
                yield return evt;
            }

            yield break;
        }

        using var scope = _scopeFactory.CreateScope();
        var sessionEvents = scope.ServiceProvider.GetRequiredService<ISessionEventRepository>();
        var events = await sessionEvents
            .AsNoTracking()
            .Where(evt => evt.ExecutionPackageId == taskId)
            .OrderBy(evt => evt.SequenceNo)
            .ToListAsync(ct);

        foreach (var evt in events)
        {
            yield return evt;
        }
    }

    public void Dispose()
    {
        foreach (var (_, stream) in _streams)
        {
            stream.Dispose();
        }

        _streams.Clear();
    }

    private void ScheduleCleanup(Guid taskId, long version, TimeSpan delay)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);
            if (_streams.TryGetValue(taskId, out var stream) && stream.Version == version)
            {
                if (_streams.TryRemove(taskId, out var removed))
                {
                    removed.Dispose();
                    _logger.LogDebug("Task stream {TaskId} cleaned up", taskId);
                }
            }
        });
    }

    private static SessionEvent CloneEvent(SessionEvent evt)
    {
        return new SessionEvent
        {
            Id = evt.Id,
            SessionId = evt.SessionId,
            FrameId = evt.FrameId,
            MessageId = evt.MessageId,
            ExecutionPackageId = evt.ExecutionPackageId,
            SourceFrameId = evt.SourceFrameId,
            SourceEffectIndex = evt.SourceEffectIndex,
            EventType = evt.EventType,
            Payload = evt.Payload,
            SequenceNo = evt.SequenceNo,
            CreatedAt = evt.CreatedAt
        };
    }
}

internal sealed class TaskStream : IDisposable
{
    private readonly ReplaySubject<SessionEvent> _subject = new();
    private readonly List<SessionEvent> _buffer = new();
    private readonly object _lock = new();
    private long _localSequenceNo;

    public TaskStream(Guid taskId)
    {
        TaskId = taskId;
    }

    public Guid TaskId { get; }

    public long Version { get; private set; }

    public void Push(SessionEvent evt)
    {
        lock (_lock)
        {
            if (evt.SequenceNo == 0)
                evt.SequenceNo = Interlocked.Increment(ref _localSequenceNo);

            Version++;
            _buffer.Add(evt);
        }

        _subject.OnNext(evt);
    }

    public void Complete() => _subject.OnCompleted();

    public async IAsyncEnumerable<SessionEvent> AsAsyncEnumerable(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var queue = new ConcurrentQueue<SessionEvent>();
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
                if (error != null)
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
