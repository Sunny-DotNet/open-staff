using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenHub.Agents;
using OpenHub.Agents.Models;
using System.Text.Json;
using TaskStatus = OpenHub.Agents.Models.TaskStatus;

namespace OpenStaff.Agents;

internal sealed class ContextualAIAgentTaskAgent : TaskAgentBase
{
    private readonly AIAgent _agent;
    private readonly IReadOnlyList<ChatMessage> _messages;
    private readonly AgentSession? _session;
    private readonly AgentRunOptions? _options;

    public ContextualAIAgentTaskAgent(
        AIAgent agent,
        IReadOnlyList<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _messages = messages?.ToArray() ?? throw new ArgumentNullException(nameof(messages));
        _session = session;
        _options = options;
    }

    public override Task<CreateTaskResponse> CreateTaskAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("A task message is required.", nameof(request));

        if (_messages.Count == 0)
            throw new InvalidOperationException("Contextual task execution requires at least one prepared chat message.");

        var taskId = Guid.NewGuid();
        var subscriber = new ContextualAIAgentTaskSubscriber(taskId);
        _taskSubscribers[taskId] = subscriber;
        Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.Pending, DateTime.UtcNow));

        var execution = Task.Run(() => ExecuteTaskAsync(taskId, subscriber, _disposeCancellationSource.Token));
        _taskExecutions[taskId] = execution;
        _ = execution.ContinueWith(
            _ => CleanupTask(taskId),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return Task.FromResult(new CreateTaskResponse(taskId, subscriber));
    }

    private async Task ExecuteTaskAsync(
        Guid taskId,
        ContextualAIAgentTaskSubscriber subscriber,
        CancellationToken cancellationToken)
    {
        try
        {
            Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.InProgress, DateTime.UtcNow));

            await foreach (var update in _agent.RunStreamingAsync(
                _messages,
                _session,
                _options,
                cancellationToken).WithCancellation(cancellationToken))
            {
                subscriber.Update(update);
            }

            subscriber.Complete();
            Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.Completed, DateTime.UtcNow));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            subscriber.Cancel();
            Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.Cancelled, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            subscriber.Throw(ex);
            Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.Failed, DateTime.UtcNow));
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposeCancellationSource.IsCancellationRequested)
            return;

        _disposeCancellationSource.Cancel();

        var runningTasks = _taskExecutions.Values.ToArray();
        if (runningTasks.Length > 0)
            await Task.WhenAll(runningTasks);

        _disposeCancellationSource.Dispose();
        await base.DisposeAsync();
    }
}

internal sealed class ContextualAIAgentTaskSubscriber : TaskSubscriberBase
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, string> _toolCallNames = new(StringComparer.Ordinal);
    private string _lastType = string.Empty;

    public ContextualAIAgentTaskSubscriber(Guid taskId) : base(taskId)
    {
    }

    private static string GetReasoningDisplayText(TextReasoningContent reasoning)
        => !string.IsNullOrWhiteSpace(reasoning.Text)
            ? reasoning.Text
            : !string.IsNullOrWhiteSpace(reasoning.ProtectedData)
                ? "[protected reasoning]"
                : string.Empty;

    public void Update(AgentResponseUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        lock (_syncRoot)
        {
            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case TextReasoningContent textReasoningContent:
                        if (!_lastType.Equals(nameof(TextReasoningContent), StringComparison.Ordinal))
                        {
                            _lastType = nameof(TextReasoningContent);
                            _reasoningNumber++;
                            _reasoningSeq = 0;
                            NewPartSubject.OnNext(_lastType);
                        }

                        TaskReasoningChunkSubject.OnNext(
                            new TaskReasoningChunkEvent(TaskId, _reasoningNumber, _reasoningSeq++, GetReasoningDisplayText(textReasoningContent)));
                        break;

                    case TextContent textContent:
                        if (!_lastType.Equals(nameof(TextContent), StringComparison.Ordinal))
                        {
                            _lastType = nameof(TextContent);
                            _contentNumber++;
                            _contentSeq = 0;
                            NewPartSubject.OnNext(_lastType);
                        }

                        TaskContentChunkSubject.OnNext(
                            new TaskContentChunkEvent(TaskId, _contentNumber, _contentSeq++, textContent.Text));
                        break;

                    case ToolCallContent toolCallContent:
                        if (!_lastType.Equals(nameof(ToolCallContent), StringComparison.Ordinal))
                        {
                            _lastType = nameof(ToolCallContent);
                            _toolCallNumber++;
                            NewPartSubject.OnNext(_lastType);
                        }

                        if (toolCallContent is FunctionCallContent functionCallContent)
                        {
                            _toolCallNames[toolCallContent.CallId] = functionCallContent.Name;
                            TaskToolCallRequestSubject.OnNext(
                                new TaskToolCallRequestEvent(
                                    TaskId,
                                    toolCallContent.CallId,
                                    functionCallContent.Name,
                                    JsonSerializer.Serialize(functionCallContent.Arguments)));
                        }

                        break;

                    case ToolResultContent toolResultContent:
                        if (toolResultContent is FunctionResultContent functionResultContent)
                        {
                            _toolCallNames.TryGetValue(functionResultContent.CallId, out var toolName);
                            _toolCallNames.Remove(functionResultContent.CallId);

                            TaskToolCallResponseSubject.OnNext(
                                new TaskToolCallResponseEvent(
                                    TaskId,
                                    functionResultContent.CallId,
                                    toolName ?? string.Empty,
                                    JsonSerializer.Serialize(functionResultContent.Result)));
                        }

                        break;

                    case UsageContent usageContent:
                        _lastType = nameof(UsageContent);
                        TaskUsageUpdatedSubject.OnNext(new TaskUsageUpdatedEvent(TaskId, usageContent));
                        break;
                }
            }
        }
    }

    public void Complete()
    {
        lock (_syncRoot)
        {
            CompleteSubscribers();
        }
    }

    public void Cancel()
    {
        lock (_syncRoot)
        {
            CancelSubscribers();
        }
    }

    public void Throw(Exception exception)
    {
        lock (_syncRoot)
        {
            FailSubscribers(exception);
        }
    }
}
