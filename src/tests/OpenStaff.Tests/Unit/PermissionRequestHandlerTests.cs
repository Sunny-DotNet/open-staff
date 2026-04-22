using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenStaff.Agent.Services;
using OpenStaff.Entities;
using OpenStaff.Core.Notifications;
using OpenStaff.HttpApi.Controllers;

namespace OpenStaff.Tests.Unit;

public sealed class PermissionRequestHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldTimeout_WhenNoListenersExist()
    {
        var notifications = new RecordingNotificationService();
        var handler = CreateHandler(
            notifications,
            new PermissionRequestHandlerOptions
            {
                ResponseTimeout = TimeSpan.FromMilliseconds(40),
                ListenerLeaseTtl = TimeSpan.FromMinutes(2)
            });

        var result = await handler.HandleAsync(new PermissionAuthorizationRequest
        {
            RequestId = "request-1",
            Kind = "write",
            Message = "允许写入文件"
        });

        Assert.Equal(PermissionAuthorizationKind.Reject, result.Kind);
        Assert.Equal(PermissionAuthorizationSource.Timeout, result.Source);
        Assert.Collection(
            notifications.Messages,
            item => Assert.Equal(PermissionNotificationEventTypes.Requested, item.EventType),
            item =>
            {
                Assert.Equal(PermissionNotificationEventTypes.Resolved, item.EventType);
                var payload = Assert.IsType<PermissionResolutionNotification>(item.Payload);
                Assert.Equal("reject", payload.Kind);
                Assert.Equal(nameof(PermissionAuthorizationSource.Timeout), payload.Source);
            });
    }

    [Fact]
    public async Task HandleAsync_ShouldUseFirstSuccessfulListener_AndIgnoreFaults()
    {
        var notifications = new RecordingNotificationService();
        var handler = CreateHandler(notifications);

        handler.Register(async (_, cancellationToken) =>
        {
            await Task.Delay(10, cancellationToken);
            throw new InvalidOperationException("boom");
        });
        handler.Register(async (_, cancellationToken) =>
        {
            await Task.Delay(20, cancellationToken);
            return new PermissionAuthorizationResult(PermissionAuthorizationKind.Accept);
        });

        var result = await handler.HandleAsync(new PermissionAuthorizationRequest
        {
            RequestId = "request-2",
            Kind = "shell",
            Message = "允许执行命令"
        });

        Assert.Equal(PermissionAuthorizationKind.Accept, result.Kind);
        Assert.Equal(PermissionAuthorizationSource.InteractiveListener, result.Source);
        var notification = Assert.Single(notifications.Messages);
        Assert.Equal(PermissionNotificationEventTypes.Resolved, notification.EventType);
        var payload = Assert.IsType<PermissionResolutionNotification>(notification.Payload);
        Assert.Equal("accept", payload.Kind);
    }

    [Fact]
    public async Task HandleAsync_ShouldCompleteFromRegisteredClientListener()
    {
        var notifications = new RecordingNotificationService();
        var handler = CreateHandler(notifications);
        handler.RegisterClientListener("listener-1");

        var handleTask = handler.HandleAsync(new PermissionAuthorizationRequest
        {
            RequestId = "request-3",
            Kind = "mcp",
            Message = "允许调用 MCP 工具",
            SessionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222")
        });

        await AssertEventuallyAsync(() => notifications.Messages.Any(item => item.EventType == PermissionNotificationEventTypes.Requested));

        var submit = await handler.SubmitAsync(new PermissionAuthorizationResponse(
            "request-3",
            PermissionAuthorizationKind.Accept,
            "listener-1"));

        var result = await handleTask;

        Assert.True(submit.Accepted);
        Assert.Equal("accepted", submit.Status);
        Assert.Equal(PermissionAuthorizationKind.Accept, result.Kind);
        Assert.Equal(PermissionAuthorizationSource.InteractiveClient, result.Source);
        Assert.Equal("listener-1", result.ListenerId);
        Assert.Equal(6, notifications.Messages.Count);
        Assert.Contains(notifications.Messages, item => item.Channel == "global" && item.EventType == PermissionNotificationEventTypes.Requested);
        Assert.Contains(notifications.Messages, item => item.Channel == "global" && item.EventType == PermissionNotificationEventTypes.Resolved);
        Assert.Contains(notifications.Messages, item => item.Channel == "session:11111111-1111-1111-1111-111111111111" && item.EventType == PermissionNotificationEventTypes.Requested);
        Assert.Contains(notifications.Messages, item => item.Channel == "session:11111111-1111-1111-1111-111111111111" && item.EventType == PermissionNotificationEventTypes.Resolved);
        Assert.Contains(notifications.Messages, item => item.Channel == "project:22222222-2222-2222-2222-222222222222" && item.EventType == PermissionNotificationEventTypes.Requested);
        Assert.Contains(notifications.Messages, item => item.Channel == "project:22222222-2222-2222-2222-222222222222" && item.EventType == PermissionNotificationEventTypes.Resolved);
    }

    [Fact]
    public async Task HandleAsync_ShouldAllowLateListenerRegistrationToResolvePendingRequest()
    {
        var notifications = new RecordingNotificationService();
        var handler = CreateHandler(notifications);

        var handleTask = handler.HandleAsync(new PermissionAuthorizationRequest
        {
            RequestId = "request-late",
            Kind = "shell",
            Message = "允许执行命令"
        });

        await AssertEventuallyAsync(() => notifications.Messages.Any(item => item.EventType == PermissionNotificationEventTypes.Requested));

        var lease = handler.RegisterClientListener("listener-late");
        var pending = Assert.Single(lease.PendingRequests ?? []);
        Assert.Equal("request-late", pending.RequestId);

        var submit = await handler.SubmitAsync(new PermissionAuthorizationResponse(
            "request-late",
            PermissionAuthorizationKind.Accept,
            "listener-late"));

        var result = await handleTask;

        Assert.True(submit.Accepted);
        Assert.Equal("accepted", submit.Status);
        Assert.Equal(PermissionAuthorizationKind.Accept, result.Kind);
        Assert.Equal(PermissionAuthorizationSource.InteractiveClient, result.Source);
        Assert.Equal("listener-late", result.ListenerId);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectOnTimeout_WhenClientDoesNotRespond()
    {
        var notifications = new RecordingNotificationService();
        var handler = CreateHandler(
            notifications,
            new PermissionRequestHandlerOptions
            {
                ResponseTimeout = TimeSpan.FromMilliseconds(40),
                ListenerLeaseTtl = TimeSpan.FromMinutes(2)
            });
        handler.RegisterClientListener("listener-timeout");

        var result = await handler.HandleAsync(new PermissionAuthorizationRequest
        {
            RequestId = "request-4",
            Kind = "url",
            Message = "允许访问 URL"
        });

        Assert.Equal(PermissionAuthorizationKind.Reject, result.Kind);
        Assert.Equal(PermissionAuthorizationSource.Timeout, result.Source);
        Assert.Collection(
            notifications.Messages,
            item => Assert.Equal(PermissionNotificationEventTypes.Requested, item.EventType),
            item =>
            {
                Assert.Equal(PermissionNotificationEventTypes.Resolved, item.EventType);
                var payload = Assert.IsType<PermissionResolutionNotification>(item.Payload);
                Assert.Equal("reject", payload.Kind);
                Assert.Equal(nameof(PermissionAuthorizationSource.Timeout), payload.Source);
            });
    }

    [Fact]
    public async Task Controller_ShouldRegisterAndSubmitResponses()
    {
        var notifications = new RecordingNotificationService();
        var broker = CreateHandler(notifications);
        var controller = new PermissionRequestsController(broker);

        var register = controller.RegisterListener(new PermissionListenerRegistrationBody
        {
            ListenerId = "listener-controller"
        });
        var registerOk = Assert.IsType<OkObjectResult>(register.Result);
        var lease = Assert.IsType<PermissionListenerLease>(registerOk.Value);
        Assert.Equal("listener-controller", lease.ListenerId);
        Assert.Empty(lease.PendingRequests ?? []);

        var handleTask = broker.HandleAsync(new PermissionAuthorizationRequest
        {
            RequestId = "request-5",
            Kind = "read",
            Message = "允许读取文件"
        });
        await AssertEventuallyAsync(() => notifications.Messages.Any(item => item.EventType == PermissionNotificationEventTypes.Requested));

        var submit = await controller.SubmitResponse(
            "request-5",
            new PermissionRequestResponseBody
            {
                ListenerId = "listener-controller",
                Kind = "accept"
            },
            CancellationToken.None);

        var submitOk = Assert.IsType<OkObjectResult>(submit.Result);
        var submitResult = Assert.IsType<PermissionAuthorizationSubmitResult>(submitOk.Value);
        Assert.True(submitResult.Accepted);
        Assert.Equal(PermissionAuthorizationKind.Accept, (await handleTask).Kind);
    }

    [Fact]
    public async Task Controller_ShouldRejectInvalidResponseKind()
    {
        var notifications = new RecordingNotificationService();
        var broker = CreateHandler(notifications);
        var controller = new PermissionRequestsController(broker);

        var result = await controller.SubmitResponse(
            "request-6",
            new PermissionRequestResponseBody
            {
                ListenerId = "listener-controller",
                Kind = "maybe"
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static PermissionRequestHandler CreateHandler(
        RecordingNotificationService notifications,
        PermissionRequestHandlerOptions? options = null)
    {
        return new PermissionRequestHandler(
            notifications,
            NullLogger<PermissionRequestHandler>.Instance,
            Microsoft.Extensions.Options.Options.Create(options ?? new PermissionRequestHandlerOptions
            {
                ResponseTimeout = TimeSpan.FromSeconds(10),
                ListenerLeaseTtl = TimeSpan.FromMinutes(2)
            }));
    }

    private static async Task AssertEventuallyAsync(Func<bool> predicate)
    {
        for (var i = 0; i < 20; i++)
        {
            if (predicate())
                return;

            await Task.Delay(10);
        }

        Assert.True(predicate());
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<RecordedNotification> Messages { get; } = [];

        public Task NotifyAsync(string channel, string eventType, object? payload = null, CancellationToken ct = default)
        {
            Messages.Add(new RecordedNotification(channel, eventType, payload));
            return Task.CompletedTask;
        }

        public Task PublishSessionEventAsync(Guid sessionId, SessionEvent sessionEvent, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed record RecordedNotification(string Channel, string EventType, object? Payload);
}
