using Microsoft.Extensions.DependencyInjection;
using OpenHub.Agents;
using OpenHub.Agents.Models;

namespace OpenStaff.Agents;

public interface IServiceAwareTaskAgent
{
    T? GetService<T>() where T : class;
}

public sealed class FeatureTaskAgent : ServiceBase, ITaskAgent, IServiceAwareTaskAgent
{
    private readonly ITaskAgent _taskAgent;
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<object> _features;
    private readonly IAsyncDisposable? _owner;

    public FeatureTaskAgent(
        ITaskAgent taskAgent,
        IServiceProvider serviceProvider,
        IEnumerable<object>? features = null,
        IAsyncDisposable? owner = null)
        : base(serviceProvider)
    {
        _taskAgent = taskAgent;
        _serviceProvider = serviceProvider;
        _features = features?
            .Where(item => item is not null)
            .ToArray() ?? [];
        _owner = owner;
    }

    public IAgentSubscriber Subscriber => _taskAgent.Subscriber;

    public Task<CreateTaskResponse> CreateTaskAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
        => _taskAgent.CreateTaskAsync(request, cancellationToken);

    public ITaskSubscriber? GetTaskSubscriber(Guid taskId)
        => _taskAgent.GetTaskSubscriber(taskId);

    public T? GetService<T>() where T : class
    {
        if (_taskAgent is T directTaskAgent)
            return directTaskAgent;

        if (_taskAgent is IServiceAwareTaskAgent serviceAwareTaskAgent)
        {
            var nestedService = serviceAwareTaskAgent.GetService<T>();
            if (nestedService is not null)
                return nestedService;
        }

        foreach (var feature in _features)
        {
            if (feature is T typedFeature)
                return typedFeature;

            if (feature is IServiceAwareTaskAgent serviceAwareFeature)
            {
                var nestedService = serviceAwareFeature.GetService<T>();
                if (nestedService is not null)
                    return nestedService;
            }
        }

        return _serviceProvider.GetService<T>();
    }

    public async ValueTask DisposeAsync()
    {
        await _taskAgent.DisposeAsync();

        if (_owner != null && !ReferenceEquals(_owner, _taskAgent))
            await _owner.DisposeAsync();
    }
}

