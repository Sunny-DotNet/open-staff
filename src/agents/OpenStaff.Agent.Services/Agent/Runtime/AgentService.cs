using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using OpenHub.Agents.Models;
using OpenStaff.Provider;
namespace OpenStaff.Agent.Services;

/// <summary>
/// zh-CN: 为单条消息统一编排智能体创建、上下文恢复、流式事件采集与重试。
/// en: Orchestrates agent creation, context restoration, streaming event collection, and retries for a single message.
/// </summary>
public sealed class AgentService : IAgentService, IDisposable
{
    private readonly IAgentRunFactory _runFactory;
    private readonly IReadOnlyList<IAgentMessageObserver> _observers;
    private readonly AgentServiceOptions _options;
    private readonly ConcurrentDictionary<Guid, MessageHandler> _handlers = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokenSources = new();

    /// <summary>
    /// zh-CN: 使用运行工厂、事件观察者和重试配置初始化运行时服务。
    /// en: Initializes the runtime service with a run factory, observers, and retry settings.
    /// </summary>
    public AgentService(
        IAgentRunFactory runFactory,
        IEnumerable<IAgentMessageObserver>? observers = null,
        AgentServiceOptions? options = null)
    {
        _runFactory = runFactory ?? throw new ArgumentNullException(nameof(runFactory));
        _observers = observers?.ToArray() ?? [];
        _options = options ?? new AgentServiceOptions();

        if (_options.MaxRetryCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MaxRetryCount must be greater than 0.");
    }

    /// <summary>
    /// zh-CN: 接收一条消息并启动后台执行。
    /// en: Accepts a message and starts background execution.
    /// </summary>
    public async Task<CreateMessageResponse> CreateMessageAsync(
        CreateMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var messageId = request.MessageId ?? Guid.NewGuid();
        var handler = new MessageHandler(messageId, request.Scene, request.MessageContext);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (!_handlers.TryAdd(messageId, handler))
        {
            linkedCts.Dispose();
            throw new InvalidOperationException($"Message handler '{messageId}' already exists.");
        }

        _cancellationTokenSources[messageId] = linkedCts;

        // zh-CN: 先发布 Accepted 事件，再异步启动真正执行，确保调用方能立刻拿到可订阅的处理器和首个状态。
        // en: Publish the Accepted event before background execution so callers immediately receive a subscribable handler and initial state.
        var acceptedEvent = new AgentMessageEvent
        {
            MessageId = messageId,
            EventType = AgentMessageEventType.Accepted,
            Scene = request.Scene,
            Context = request.MessageContext,
            OccurredAt = DateTimeOffset.UtcNow
        };

        try
        {
            await PublishAsync(handler, acceptedEvent, cancellationToken);
        }
        catch
        {
            RemoveMessageHandler(messageId);
            throw;
        }

        var normalizedRequest = request with { MessageId = messageId };
        // zh-CN: 执行管线故意脱离调用线程，外层 API 在 Accepted 后即可返回。
        // en: The execution pipeline intentionally detaches from the request thread so the outer API can return immediately after acceptance.
        _ = Task.Run(() => ExecuteAsync(normalizedRequest, handler, linkedCts), CancellationToken.None);

        return new CreateMessageResponse(messageId);
    }

    /// <summary>
    /// zh-CN: 尝试获取指定消息的活动处理器。
    /// en: Tries to get the active handler for the specified message.
    /// </summary>
    public bool TryGetMessageHandler(Guid messageId, out MessageHandler? handler)
        => _handlers.TryGetValue(messageId, out handler);

    /// <summary>
    /// zh-CN: 取消指定消息的执行。
    /// en: Cancels execution for the specified message.
    /// </summary>
    public Task<bool> CancelMessageAsync(Guid messageId)
    {
        if (!_cancellationTokenSources.TryGetValue(messageId, out var cancellationTokenSource))
            return Task.FromResult(false);

        cancellationTokenSource.Cancel();
        return Task.FromResult(true);
    }

    /// <summary>
    /// zh-CN: 移除消息处理器并释放其关联资源。
    /// en: Removes the message handler and releases associated resources.
    /// </summary>
    public bool RemoveMessageHandler(Guid messageId)
    {
        var removed = _handlers.TryRemove(messageId, out var handler);

        if (_cancellationTokenSources.TryRemove(messageId, out var cancellationTokenSource))
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        handler?.Dispose();
        return removed;
    }

    /// <summary>
    /// zh-CN: 释放所有仍在跟踪中的消息处理器。
    /// en: Releases all message handlers that are still being tracked.
    /// </summary>
    public void Dispose()
    {
        foreach (var messageId in _handlers.Keys.ToArray())
            RemoveMessageHandler(messageId);
    }

    /// <summary>
    /// zh-CN: 为已接受的消息执行完整运行管线，负责准备、流式采集、重试以及在任何退出路径上完成本地终态。
    /// en: Runs the full pipeline for an accepted message, covering preparation, streaming collection, retries, and guaranteed local terminal completion on every exit path.
    /// </summary>
    private async Task ExecuteAsync(
        CreateMessageRequest request,
        MessageHandler handler,
        CancellationTokenSource linkedCts)
    {
        MessageExecutionSummary? terminalSummary = null;
        PreparedAgentRun? preparedRun = null;
        try
        {
            preparedRun = await _runFactory.PrepareAsync(request, handler.MessageId, linkedCts.Token);

            for (var attempt = 1; attempt <= _options.MaxRetryCount; attempt++)
            {
                var stopwatch = Stopwatch.StartNew();
                var state = new AgentExecutionState(
                    handler.MessageId,
                    request.Scene,
                    request.MessageContext,
                    attempt,
                    preparedRun.AgentRole,
                    preparedRun.Model);

                try
                {
                    await PublishAsync(handler, state.CreateLifecycleEvent(AgentMessageEventType.Started), linkedCts.Token);

                    var createTaskResponse = await preparedRun.Agent.CreateTaskAsync(
                        new CreateTaskRequest(request.Input),
                        linkedCts.Token);
                    var publishChannel = Channel.CreateUnbounded<AgentMessageEvent>(new UnboundedChannelOptions
                    {
                        SingleReader = true,
                        SingleWriter = false
                    });
                    var publishTask = PumpTaskEventsAsync(handler, publishChannel.Reader, linkedCts.Token);

                    void QueueEvent(AgentMessageEvent? messageEvent)
                    {
                        if (messageEvent is null)
                            return;

                        if (!publishChannel.Writer.TryWrite(messageEvent))
                        {
                            publishChannel.Writer.TryComplete(
                                new InvalidOperationException("Failed to queue task-agent event for publication."));
                        }
                    }

                    using var reasoningSubscription = createTaskResponse.Subscriber.TaskReasoningChunk.Subscribe(
                        chunk => QueueEvent(state.Collect(chunk)));
                    using var contentSubscription = createTaskResponse.Subscriber.TaskContentChunk.Subscribe(
                        chunk => QueueEvent(state.Collect(chunk, stopwatch)));
                    using var toolCallSubscription = createTaskResponse.Subscriber.TaskToolCallRequest.Subscribe(
                        chunk => QueueEvent(state.Collect(chunk)));
                    using var toolResultSubscription = createTaskResponse.Subscriber.TaskToolCallResponse.Subscribe(
                        chunk => QueueEvent(state.Collect(chunk)));
                    using var usageSubscription = createTaskResponse.Subscriber.TaskUsageUpdated.Subscribe(
                        chunk => QueueEvent(state.Collect(chunk)));

                    try
                    {
                        await createTaskResponse.Subscriber.WaitForCompletionAsync(linkedCts.Token);
                    }
                    finally
                    {
                        publishChannel.Writer.TryComplete();
                        await publishTask;
                    }

                    stopwatch.Stop();
                    terminalSummary = state.CreateSummary(success: true, cancelled: false, error: null, stopwatch);
                    await PublishTerminalAsync(handler, state.CreateCompletedEvent(stopwatch), CancellationToken.None);
                    return;
                }
                catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
                {
                    stopwatch.Stop();
                    terminalSummary = state.CreateSummary(
                        success: false,
                        cancelled: true,
                        error: "Message execution cancelled.",
                        stopwatch);
                    await PublishTerminalAsync(handler, state.CreateCancelledEvent(stopwatch), CancellationToken.None);
                    return;
                }
                catch (Exception ex) when (attempt < _options.MaxRetryCount)
                {
                    stopwatch.Stop();
                    // zh-CN: Retry 事件先进入本地 replay，再扇出到观察者，这样订阅方不会因外部投递延迟而错过 attempt 切换。
                    // en: The retry event is written to local replay before observer fan-out so subscribers see the attempt switch immediately even if external delivery lags.
                    await PublishAsync(handler, state.CreateRetryEvent(ex, stopwatch), linkedCts.Token);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    terminalSummary = state.CreateSummary(
                        success: false,
                        cancelled: false,
                        error: ex.Message,
                        stopwatch);
                    await PublishTerminalAsync(handler, state.CreateErrorEvent(ex, stopwatch), CancellationToken.None);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // zh-CN: Prepare 阶段就被取消时还没有可复用的 AgentExecutionState，因此直接构造最小终态摘要。
            // en: If cancellation happens during preparation there is no reusable AgentExecutionState, so emit a minimal terminal summary directly.
            terminalSummary = new MessageExecutionSummary(
                handler.MessageId,
                handler.Scene,
                handler.Context,
                false,
                true,
                0,
                null,
                null,
                string.Empty,
                string.Empty,
                null,
                null,
                [],
                "Message execution cancelled before startup.");

            await PublishHandlerOnlyAsync(handler, new AgentMessageEvent
            {
                MessageId = handler.MessageId,
                EventType = AgentMessageEventType.Cancelled,
                Scene = handler.Scene,
                Context = handler.Context,
                OccurredAt = DateTimeOffset.UtcNow,
                IsTerminal = true,
                Error = terminalSummary.Error
            });
        }
        catch (Exception ex)
        {
            // zh-CN: 如果准备阶段抛错，仍然要让本地订阅方收到终态，避免 Completion 永远等待。
            // en: Preparation failures still need to produce a terminal local event so subscribers do not wait on Completion forever.
            terminalSummary = new MessageExecutionSummary(
                handler.MessageId,
                handler.Scene,
                handler.Context,
                false,
                false,
                0,
                null,
                null,
                string.Empty,
                string.Empty,
                null,
                null,
                [],
                ex.Message);

            await PublishHandlerOnlyAsync(handler, new AgentMessageEvent
            {
                MessageId = handler.MessageId,
                EventType = AgentMessageEventType.Error,
                Scene = handler.Scene,
                Context = handler.Context,
                OccurredAt = DateTimeOffset.UtcNow,
                IsTerminal = true,
                Error = ex.Message
            });
        }
        finally
        {
            if (preparedRun?.ExecutionLease != null)
                await preparedRun.ExecutionLease.DisposeAsync();

            if (preparedRun?.Agent is not null)
                await preparedRun.Agent.DisposeAsync();

            if (_cancellationTokenSources.TryRemove(handler.MessageId, out var cancellationTokenSource))
                cancellationTokenSource.Dispose();

            if (terminalSummary is not null)
                handler.Complete(terminalSummary);
        }
    }

    /// <summary>
    /// zh-CN: 普通事件先写入本地回放流，再扇出给观察者。
    /// en: Writes non-terminal events to local replay before fanning them out to observers.
    /// </summary>
    private async Task PublishAsync(
        MessageHandler handler,
        AgentMessageEvent messageEvent,
        CancellationToken cancellationToken)
    {
        handler.Publish(messageEvent);
        Debug.WriteLine(messageEvent.EventType);
        foreach (var observer in _observers)
            await observer.PublishAsync(messageEvent, cancellationToken);
    }

    /// <summary>
    /// zh-CN: 终态事件优先保证本地处理器完成，外部投递失败不会阻塞 Completion。
    /// en: Prioritizes completion of the local handler for terminal events even when external delivery fails.
    /// </summary>
    private async Task PublishTerminalAsync(
        MessageHandler handler,
        AgentMessageEvent messageEvent,
        CancellationToken cancellationToken)
    {
        handler.Publish(messageEvent);

        foreach (var observer in _observers)
        {
            try
            {
                await observer.PublishAsync(messageEvent, cancellationToken);
            }
            catch
            {
                // zh-CN: 本地回放已经拿到终态，这里吞掉外部失败，避免因为持久化或推送故障卡住调用方。
                // en: Local replay already has the terminal event, so swallow observer failures here to avoid blocking callers on persistence or push errors.
            }
        }
    }

    private async Task PumpTaskEventsAsync(
        MessageHandler handler,
        ChannelReader<AgentMessageEvent> reader,
        CancellationToken cancellationToken)
    {
        await foreach (var messageEvent in reader.ReadAllAsync(cancellationToken))
            await PublishAsync(handler, messageEvent, cancellationToken);
    }

    /// <summary>
    /// zh-CN: 在准备阶段失败或取消时仅写入本地回放流，确保 Completion 可结束而不会依赖外部观察者。
    /// en: Writes fallback terminal events only to the local replay stream when preparation fails or is cancelled so Completion never depends on external observers.
    /// </summary>
    private static Task PublishHandlerOnlyAsync(MessageHandler handler, AgentMessageEvent messageEvent)
    {
        handler.Publish(messageEvent);
        return Task.CompletedTask;
    }

    /// <summary>
    /// zh-CN: 提前校验输入和目标解析信息，避免运行时在后台线程里才暴露不可恢复的请求错误。
    /// en: Validates input and target resolution hints up front so unrecoverable request errors do not surface only after the work has moved to a background thread.
    /// </summary>
    private static void ValidateRequest(CreateMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
            throw new ArgumentException("Input is required.", nameof(request));

        if (request.AgentRoleId == null
            && request.MessageContext.ProjectAgentRoleId == null
            && string.IsNullOrWhiteSpace(request.MessageContext.TargetRole))
        {
            throw new ArgumentException("AgentRoleId, TargetRole or ProjectAgentRoleId is required.", nameof(request));
        }
    }
}
