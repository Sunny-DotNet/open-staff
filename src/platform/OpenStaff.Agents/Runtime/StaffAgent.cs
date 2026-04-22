using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenHub.Agents;
using OpenHub.Agents.Models;

namespace OpenStaff.Agents;

public interface IStaffAgent : IAsyncDisposable
{
    IAgentSubscriber Subscriber { get; }
    ITaskSubscriber? GetTaskSubscriber(Guid taskId);
    Task<CreateTaskResponse> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
    T? GetService<T>() where T : class;
}

public abstract class StaffAgentBase : ServiceBase, IStaffAgent
{
    private readonly ITaskAgent _taskAgent;
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<object> _features;
    private readonly IAsyncDisposable? _owner;

    protected StaffAgentBase(
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

    protected ITaskAgent InnerTaskAgent => _taskAgent;
    protected IServiceProvider ServiceProvider => _serviceProvider;

    public virtual IAgentSubscriber Subscriber => _taskAgent.Subscriber;

    public virtual Task<CreateTaskResponse> CreateTaskAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
        => _taskAgent.CreateTaskAsync(request, cancellationToken);

    public ITaskSubscriber? GetTaskSubscriber(Guid taskId)
        => _taskAgent.GetTaskSubscriber(taskId);

    public virtual T? GetService<T>() where T : class
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

            if (feature is IStaffAgent staffAgent)
            {
                var nestedService = staffAgent.GetService<T>();
                if (nestedService is not null)
                    return nestedService;
            }
        }

        return _serviceProvider.GetService<T>();
    }

    public virtual async ValueTask DisposeAsync()
    {
        await _taskAgent.DisposeAsync();

        if (_owner != null && !ReferenceEquals(_owner, _taskAgent))
            await _owner.DisposeAsync();
    }
}

internal sealed class DefaultStaffAgent : StaffAgentBase
{
    public DefaultStaffAgent(
        ITaskAgent taskAgent,
        IServiceProvider serviceProvider,
        IEnumerable<object>? features = null,
        IAsyncDisposable? owner = null)
        : base(taskAgent, serviceProvider, features, owner)
    {
    }
}

public static class StaffAgentExtensions
{
    public static ITaskAgent AsFeatureTaskAgent(
        this ITaskAgent taskAgent,
        IServiceProvider serviceProvider,
        params object[] features)
        => new FeatureTaskAgent(taskAgent, serviceProvider, features);

    public static IStaffAgent AsStaffAgent(
        this ITaskAgent taskAgent,
        IServiceProvider serviceProvider,
        params object[] features)
        => new DefaultStaffAgent(taskAgent, serviceProvider, features);

    public static IStaffAgent AsStaffAgent(
        this AIAgent agent,
        IServiceProvider serviceProvider,
        params object[] features)
    {
        ITaskAgent taskAgent = agent.AsTaskAgent();
        return taskAgent.AsStaffAgent(serviceProvider, MergeFeatures(agent, features));
    }

    public static IStaffAgent AsContextualStaffAgent(
        this IStaffAgent agent,
        IServiceProvider serviceProvider,
        IReadOnlyList<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null)
    {
        var aiAgent = agent.GetService<AIAgent>()
            ?? throw new InvalidOperationException(
                $"Staff agent '{agent.GetType().Name}' does not expose an {nameof(AIAgent)} for contextual execution.");

        ITaskAgent taskAgent = new ContextualAIAgentTaskAgent(aiAgent, messages, session, options);
        return new DefaultStaffAgent(taskAgent, serviceProvider, [agent, aiAgent], owner: agent);
    }

    private static object[] MergeFeatures(object firstFeature, object[] features)
    {
        if (features.Length == 0)
            return [firstFeature];

        var merged = new object[features.Length + 1];
        merged[0] = firstFeature;
        Array.Copy(features, 0, merged, 1, features.Length);
        return merged;
    }
}
