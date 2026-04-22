using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenStaff;
using OpenStaff.Core.Notifications;

namespace OpenStaff.Agent.Services;

public interface IPermissionRequestHandler
{
    Task<PermissionAuthorizationResult> HandleAsync(PermissionAuthorizationRequest request, CancellationToken cancellationToken = default);

    Task<PermissionAuthorizationSubmitResult> SubmitAsync(PermissionAuthorizationResponse response, CancellationToken cancellationToken = default);

    PermissionListenerLease RegisterClientListener(string? listenerId = null);

    void UnregisterClientListener(string listenerId);

    IDisposable Register(Func<PermissionAuthorizationRequest, CancellationToken, Task<PermissionAuthorizationResult?>> handler);
}

public sealed class PermissionRequestHandlerOptions
{
    public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan ListenerLeaseTtl { get; set; } = TimeSpan.FromMinutes(2);
}

public enum PermissionAuthorizationKind
{
    Accept,
    Reject
}

public enum PermissionAuthorizationSource
{
    InteractiveClient,
    InteractiveListener,
    NoListener,
    Timeout,
    Cancelled
}

public sealed record PermissionAuthorizationRequest
{
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");

    public string Kind { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public Guid? SessionId { get; init; }

    public Guid? ProjectId { get; init; }

    public Guid? AgentInstanceId { get; init; }

    public Guid? ProjectAgentRoleId { get; init; }

    public string? CopilotSessionId { get; init; }

    public string? RoleName { get; init; }

    public string? ProjectName { get; init; }

    public string? Scene { get; init; }

    public string? DispatchSource { get; init; }

    public string? ToolCallId { get; init; }

    public string? ToolName { get; init; }

    public string? FileName { get; init; }

    public string? Url { get; init; }

    public string? CommandText { get; init; }

    public string? Warning { get; init; }

    public string? DetailsJson { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public int? TimeoutMs { get; init; }
}

public readonly record struct PermissionAuthorizationResult(
    PermissionAuthorizationKind Kind,
    PermissionAuthorizationSource Source = PermissionAuthorizationSource.InteractiveClient,
    string? ListenerId = null);

public sealed record PermissionAuthorizationResponse(
    string RequestId,
    PermissionAuthorizationKind Kind,
    string ListenerId);

public sealed record PermissionAuthorizationSubmitResult(
    string RequestId,
    string Status,
    bool Accepted);

public sealed record PermissionListenerLease(
    string ListenerId,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<PermissionRequestNotification>? PendingRequests = null);

public sealed record PermissionRequestNotification
{
    public required string RequestId { get; init; }

    public required string Kind { get; init; }

    public required string Message { get; init; }

    public Guid? SessionId { get; init; }

    public Guid? ProjectId { get; init; }

    public Guid? AgentInstanceId { get; init; }

    public Guid? ProjectAgentRoleId { get; init; }

    public string? CopilotSessionId { get; init; }

    public string? RoleName { get; init; }

    public string? ProjectName { get; init; }

    public string? Scene { get; init; }

    public string? DispatchSource { get; init; }

    public string? ToolCallId { get; init; }

    public string? ToolName { get; init; }

    public string? FileName { get; init; }

    public string? Url { get; init; }

    public string? CommandText { get; init; }

    public string? Warning { get; init; }

    public string? DetailsJson { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public int TimeoutMs { get; init; }
}

public sealed record PermissionResolutionNotification
{
    public required string RequestId { get; init; }

    public required string Kind { get; init; }

    public required string Source { get; init; }

    public string? ListenerId { get; init; }

    public Guid? SessionId { get; init; }

    public Guid? ProjectId { get; init; }
}

public static class PermissionNotificationEventTypes
{
    public const string Requested = "permission_requested";
    public const string Resolved = "permission_resolved";
}

public sealed class PermissionRequestHandler : IPermissionRequestHandler
{
    private readonly ConcurrentDictionary<Guid, RegisteredHandler> _handlers = new();
    private readonly ConcurrentDictionary<string, RegisteredClientListener> _clientListeners = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PendingPermissionRequest> _pendingRequests = new(StringComparer.Ordinal);
    private readonly INotificationService _notificationService;
    private readonly ILogger<PermissionRequestHandler> _logger;
    private readonly PermissionRequestHandlerOptions _options;

    public PermissionRequestHandler(
        INotificationService notificationService,
        ILogger<PermissionRequestHandler> logger,
        IOptions<PermissionRequestHandlerOptions> options)
    {
        _notificationService = notificationService;
        _logger = logger;
        _options = options.Value;
    }

    public IDisposable Register(Func<PermissionAuthorizationRequest, CancellationToken, Task<PermissionAuthorizationResult?>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var registrationId = Guid.NewGuid();
        _handlers[registrationId] = new RegisteredHandler(handler);
        return DisposeAction.Create(() => _handlers.TryRemove(registrationId, out _));
    }

    public PermissionListenerLease RegisterClientListener(string? listenerId = null)
    {
        var id = string.IsNullOrWhiteSpace(listenerId)
            ? Guid.NewGuid().ToString("N")
            : listenerId.Trim();
        var expiresAt = DateTimeOffset.UtcNow.Add(_options.ListenerLeaseTtl);

        _clientListeners.AddOrUpdate(
            id,
            _ => new RegisteredClientListener(expiresAt),
            (_, _) => new RegisteredClientListener(expiresAt));

        return new PermissionListenerLease(id, expiresAt, GetPendingRequestNotifications());
    }

    public void UnregisterClientListener(string listenerId)
    {
        if (string.IsNullOrWhiteSpace(listenerId))
            return;

        _clientListeners.TryRemove(listenerId, out _);
    }

    public async Task<PermissionAuthorizationResult> HandleAsync(
        PermissionAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        PruneExpiredListeners();
        var normalizedRequest = NormalizeRequest(request);
        var registeredHandlers = _handlers.Values.Select(item => item.Handler).ToArray();
        var autoResolved = await TryResolveFromRegisteredHandlersAsync(
            registeredHandlers,
            normalizedRequest,
            cancellationToken).ConfigureAwait(false);
        if (autoResolved.HasValue)
        {
            await PublishResolutionNotificationAsync(normalizedRequest, autoResolved.Value, cancellationToken).ConfigureAwait(false);
            return autoResolved.Value;
        }

        var pending = new PendingPermissionRequest(normalizedRequest);
        if (!_pendingRequests.TryAdd(normalizedRequest.RequestId, pending))
            throw new InvalidOperationException($"A pending permission request with id '{normalizedRequest.RequestId}' already exists.");

        using var timeoutCts = CreateTimeoutCancellationSource(normalizedRequest);
        using var timeoutRegistration = timeoutCts.Token.Register(() =>
            _ = ResolveAsync(
                pending,
                new PermissionAuthorizationResult(
                    PermissionAuthorizationKind.Reject,
                    PermissionAuthorizationSource.Timeout)));
        using var requestCancellation = cancellationToken.Register(() =>
            _ = ResolveAsync(
                pending,
                new PermissionAuthorizationResult(
                    PermissionAuthorizationKind.Reject,
                    PermissionAuthorizationSource.Cancelled)));

        try
        {
            await PublishNotificationAsync(
                normalizedRequest,
                PermissionNotificationEventTypes.Requested,
                ToNotification(normalizedRequest),
                cancellationToken).ConfigureAwait(false);

            return await pending.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            _pendingRequests.TryRemove(normalizedRequest.RequestId, out _);
            pending.Cancellation.Cancel();
            pending.Cancellation.Dispose();
        }
    }

    public async Task<PermissionAuthorizationSubmitResult> SubmitAsync(
        PermissionAuthorizationResponse response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (string.IsNullOrWhiteSpace(response.ListenerId))
            return new PermissionAuthorizationSubmitResult(response.RequestId, "listener_not_registered", false);

        PruneExpiredListeners();
        if (!_clientListeners.ContainsKey(response.ListenerId))
            return new PermissionAuthorizationSubmitResult(response.RequestId, "listener_not_registered", false);

        if (!_pendingRequests.TryGetValue(response.RequestId, out var pending))
            return new PermissionAuthorizationSubmitResult(response.RequestId, "not_found", false);

        var accepted = await ResolveAsync(
            pending,
            new PermissionAuthorizationResult(
                response.Kind,
                PermissionAuthorizationSource.InteractiveClient,
                response.ListenerId)).ConfigureAwait(false);

        return new PermissionAuthorizationSubmitResult(
            response.RequestId,
            accepted ? "accepted" : "already_completed",
            accepted);
    }

    private PermissionAuthorizationRequest NormalizeRequest(PermissionAuthorizationRequest request)
    {
        var timeout = request.TimeoutMs is > 0
            ? request.TimeoutMs
            : Convert.ToInt32(Math.Ceiling(_options.ResponseTimeout.TotalMilliseconds));

        return request with
        {
            RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? Guid.NewGuid().ToString("N") : request.RequestId,
            CreatedAt = request.CreatedAt == default ? DateTimeOffset.UtcNow : request.CreatedAt,
            TimeoutMs = timeout
        };
    }

    private CancellationTokenSource CreateTimeoutCancellationSource(PermissionAuthorizationRequest request)
    {
        var timeout = request.TimeoutMs is > 0
            ? TimeSpan.FromMilliseconds(request.TimeoutMs.Value)
            : _options.ResponseTimeout;
        return timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan
            ? new CancellationTokenSource(timeout)
            : new CancellationTokenSource();
    }

    private async Task<PermissionAuthorizationResult?> TryResolveFromRegisteredHandlersAsync(
        Func<PermissionAuthorizationRequest, CancellationToken, Task<PermissionAuthorizationResult?>>[] handlers,
        PermissionAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
        {
            try
            {
                var result = await handler(request, cancellationToken).ConfigureAwait(false);
                if (result.HasValue)
                {
                    return result.Value with
                    {
                        Source = result.Value.Source == PermissionAuthorizationSource.InteractiveClient
                            ? PermissionAuthorizationSource.InteractiveListener
                            : result.Value.Source
                    };
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "Permission listener cancelled for request {RequestId}.",
                    request.RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Permission listener failed for request {RequestId}.",
                    request.RequestId);
            }
        }

        return null;
    }

    private async Task<bool> ResolveAsync(
        PendingPermissionRequest pending,
        PermissionAuthorizationResult result,
        CancellationToken cancellationToken = default)
    {
        if (!pending.Completion.TrySetResult(result))
            return false;

        pending.Cancellation.Cancel();
        try
        {
            await PublishResolutionNotificationAsync(pending.Request, result, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to publish permission resolution notification for request {RequestId}.",
                pending.Request.RequestId);
        }

        return true;
    }

    private IReadOnlyList<PermissionRequestNotification> GetPendingRequestNotifications()
        => _pendingRequests.Values
            .Select(item => ToNotification(item.Request))
            .OrderBy(item => item.CreatedAt)
            .ToArray();

    private void PruneExpiredListeners()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _clientListeners)
        {
            if (pair.Value.ExpiresAt <= now)
                _clientListeners.TryRemove(pair.Key, out _);
        }
    }

    private static PermissionRequestNotification ToNotification(PermissionAuthorizationRequest request)
    {
        return new PermissionRequestNotification
        {
            RequestId = request.RequestId,
            Kind = request.Kind,
            Message = request.Message,
            SessionId = request.SessionId,
            ProjectId = request.ProjectId,
            AgentInstanceId = request.AgentInstanceId,
            ProjectAgentRoleId = request.ProjectAgentRoleId,
            CopilotSessionId = request.CopilotSessionId,
            RoleName = request.RoleName,
            ProjectName = request.ProjectName,
            Scene = request.Scene,
            DispatchSource = request.DispatchSource,
            ToolCallId = request.ToolCallId,
            ToolName = request.ToolName,
            FileName = request.FileName,
            Url = request.Url,
            CommandText = request.CommandText,
            Warning = request.Warning,
            DetailsJson = request.DetailsJson,
            CreatedAt = request.CreatedAt,
            TimeoutMs = request.TimeoutMs ?? 0
        };
    }

    private static string ToWireKind(PermissionAuthorizationKind kind) => kind switch
    {
        PermissionAuthorizationKind.Accept => "accept",
        _ => "reject"
    };

    private async Task PublishResolutionNotificationAsync(
        PermissionAuthorizationRequest request,
        PermissionAuthorizationResult result,
        CancellationToken cancellationToken)
    {
        await PublishNotificationAsync(
            request,
            PermissionNotificationEventTypes.Resolved,
            new PermissionResolutionNotification
            {
                RequestId = request.RequestId,
                Kind = ToWireKind(result.Kind),
                Source = result.Source.ToString(),
                ListenerId = result.ListenerId,
                SessionId = request.SessionId,
                ProjectId = request.ProjectId
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishNotificationAsync(
        PermissionAuthorizationRequest request,
        string eventType,
        object payload,
        CancellationToken cancellationToken)
    {
        foreach (var channel in GetNotificationChannels(request))
        {
            await _notificationService.NotifyAsync(
                channel,
                eventType,
                payload,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<string> GetNotificationChannels(PermissionAuthorizationRequest request)
    {
        var channels = new List<string>(3);
        if (request.SessionId.HasValue)
            channels.Add(Channels.Session(request.SessionId.Value));
        if (request.ProjectId.HasValue)
            channels.Add(Channels.Project(request.ProjectId.Value));
        channels.Add(Channels.Global);
        return channels.Distinct(StringComparer.Ordinal).ToArray();
    }

    private sealed record RegisteredHandler(
        Func<PermissionAuthorizationRequest, CancellationToken, Task<PermissionAuthorizationResult?>> Handler);

    private sealed record RegisteredClientListener(
        DateTimeOffset ExpiresAt);

    private sealed class PendingPermissionRequest
    {
        public PendingPermissionRequest(PermissionAuthorizationRequest request)
        {
            Request = request;
        }

        public PermissionAuthorizationRequest Request { get; }

        public CancellationTokenSource Cancellation { get; } = new();

        public TaskCompletionSource<PermissionAuthorizationResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
