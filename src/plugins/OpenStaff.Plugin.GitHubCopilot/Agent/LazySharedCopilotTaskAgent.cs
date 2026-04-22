using OpenHub.Agents;
using OpenHub.Agents.Models;
using System.Reactive.Linq;

namespace OpenStaff.Agent.Vendor.GitHubCopilot;

internal sealed class LazySharedCopilotTaskAgent : ITaskAgent
{
    private readonly Func<CancellationToken, Task<ITaskAgent>> _taskAgentFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AgentSubscriber _subscriber = new();
    private ITaskAgent? _innerAgent;
    private IDisposable? _statusSubscription;
    private int _disposed;

    public LazySharedCopilotTaskAgent(Func<CancellationToken, Task<ITaskAgent>> taskAgentFactory)
    {
        _taskAgentFactory = taskAgentFactory ?? throw new ArgumentNullException(nameof(taskAgentFactory));
    }

    public IAgentSubscriber Subscriber => _subscriber;

    public ITaskSubscriber? GetTaskSubscriber(Guid taskId)
        => _innerAgent?.GetTaskSubscriber(taskId);

    public async Task<CreateTaskResponse> CreateTaskAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var taskAgent = await EnsureInnerAgentAsync(cancellationToken);
        return await taskAgent.CreateTaskAsync(request, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _statusSubscription?.Dispose();

        try
        {
            if (_innerAgent is not null)
                await _innerAgent.DisposeAsync();
        }
        finally
        {
            _subscriber.Complete();
            _gate.Dispose();
        }
    }

    private async Task<ITaskAgent> EnsureInnerAgentAsync(CancellationToken cancellationToken)
    {
        if (_innerAgent is not null)
            return _innerAgent;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            if (_innerAgent is not null)
                return _innerAgent;

            var taskAgent = await _taskAgentFactory(cancellationToken);
            _statusSubscription = taskAgent.Subscriber.TaskStatusChanged.Subscribe(
                @event => _subscriber.PublishTaskStatusChanged(@event),
                exception => _subscriber.Throw(exception));
            _innerAgent = taskAgent;
            return taskAgent;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed != 0)
            throw new ObjectDisposedException(GetType().Name);
    }
}
