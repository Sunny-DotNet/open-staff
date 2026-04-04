using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Events;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Infrastructure.Events;

/// <summary>
/// 事件发布器实现 — 持久化到数据库并广播给订阅者
/// Event publisher — persists to DB and broadcasts to subscribers
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly EventBus _eventBus;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(IServiceProvider serviceProvider, EventBus eventBus, ILogger<EventPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task PublishAsync(AgentEventData eventData, CancellationToken cancellationToken = default)
    {
        // 1. 持久化到数据库 / Persist to database
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var agentEvent = new AgentEvent
            {
                Id = Guid.NewGuid(),
                ProjectId = eventData.ProjectId,
                AgentId = eventData.AgentId,
                EventType = eventData.EventType,
                Content = eventData.Content,
                Metadata = eventData.Metadata,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.AgentEvents.Add(agentEvent);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "持久化事件失败 / Failed to persist event: {EventType}", eventData.EventType);
        }

        // 2. 广播给内存订阅者 / Broadcast to in-memory subscribers
        await _eventBus.PublishAsync(eventData);
    }
}

/// <summary>
/// 内存事件总线 — 管理订阅和广播
/// In-memory event bus — manages subscriptions and broadcasts
/// </summary>
public class EventBus : IEventSubscriber
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<AgentEventData>>> _projectChannels = new();
    private readonly ConcurrentDictionary<Guid, Channel<AgentEventData>> _globalChannels = new();

    public async Task PublishAsync(AgentEventData eventData)
    {
        // 广播给工程订阅者 / Broadcast to project subscribers
        if (_projectChannels.TryGetValue(eventData.ProjectId, out var projectSubs))
        {
            foreach (var (_, channel) in projectSubs)
            {
                await channel.Writer.WriteAsync(eventData);
            }
        }

        // 广播给全局订阅者 / Broadcast to global subscribers
        foreach (var (_, channel) in _globalChannels)
        {
            await channel.Writer.WriteAsync(eventData);
        }
    }

    public IDisposable Subscribe(Guid projectId, Func<AgentEventData, Task> handler)
    {
        var subId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<AgentEventData>();
        var subs = _projectChannels.GetOrAdd(projectId, _ => new ConcurrentDictionary<Guid, Channel<AgentEventData>>());
        subs[subId] = channel;

        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(cts.Token))
            {
                try { await handler(evt); }
                catch { /* 订阅者错误不影响其他 */ }
            }
        }, cts.Token);

        return new Subscription(() =>
        {
            cts.Cancel();
            subs.TryRemove(subId, out _);
            channel.Writer.TryComplete();
        });
    }

    public IDisposable SubscribeAll(Func<AgentEventData, Task> handler)
    {
        var subId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<AgentEventData>();
        _globalChannels[subId] = channel;

        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(cts.Token))
            {
                try { await handler(evt); }
                catch { /* 订阅者错误不影响其他 */ }
            }
        }, cts.Token);

        return new Subscription(() =>
        {
            cts.Cancel();
            _globalChannels.TryRemove(subId, out _);
            channel.Writer.TryComplete();
        });
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _onDispose;
        public Subscription(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}
