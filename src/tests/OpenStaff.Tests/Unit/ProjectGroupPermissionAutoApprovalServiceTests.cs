using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenStaff.Agent.Services;
using OpenStaff.Application.Contracts.Settings;
using OpenStaff.Application.Services;
using OpenStaff.Application.Settings.Services;
using OpenStaff.Core.Notifications;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Repositories;

namespace OpenStaff.Tests.Unit;

public sealed class ProjectGroupPermissionAutoApprovalServiceTests
{
    [Fact]
    public async Task HandleAsync_ShouldAutoApproveProjectGroupShellRequest_WhenSettingEnabled()
    {
        await using var context = new TestContext(autoApproveEnabled: true);

        var result = await context.Handler.HandleAsync(new PermissionAuthorizationRequest
        {
            RequestId = "pg-shell-1",
            Kind = "shell",
            Message = "允许执行命令",
            ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SessionId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Scene = SessionSceneTypes.ProjectGroup
        });

        Assert.Equal(PermissionAuthorizationKind.Accept, result.Kind);
        Assert.Equal(PermissionAuthorizationSource.InteractiveListener, result.Source);
        Assert.Equal("project-group-auto-approval", result.ListenerId);
        Assert.Equal(3, context.Notifications.Messages.Count);
        Assert.All(context.Notifications.Messages, item => Assert.Equal(PermissionNotificationEventTypes.Resolved, item.EventType));
    }

    [Fact]
    public async Task HandleAsync_ShouldNotAutoApproveNonCapabilityRequest_WhenSettingEnabled()
    {
        await using var context = new TestContext(autoApproveEnabled: true);

        var result = await context.Handler.HandleAsync(new PermissionAuthorizationRequest
        {
            RequestId = "pg-write-1",
            Kind = "write",
            Message = "允许写入文件",
            ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SessionId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Scene = SessionSceneTypes.ProjectGroup
        });

        Assert.Equal(PermissionAuthorizationKind.Reject, result.Kind);
        Assert.Equal(PermissionAuthorizationSource.NoListener, result.Source);
        Assert.Empty(context.Notifications.Messages);
    }

    private sealed class TestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;
        private readonly ProjectGroupPermissionAutoApprovalService _service;

        public TestContext(bool autoApproveEnabled)
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            _services = new ServiceCollection()
                .AddLogging()
                .AddDbContext<AppDbContext>(options => options.UseSqlite(_connection))
                .AddScoped<IGlobalSettingRepository, GlobalSettingRepository>()
                .AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>())
                .AddScoped<SettingsService>()
                .BuildServiceProvider();

            Db = _services.GetRequiredService<AppDbContext>();
            Db.Database.EnsureCreated();
            Db.GlobalSettings.Add(new GlobalSetting
            {
                Key = SystemSettingsKeys.ProjectGroupAutoApproveCapabilities,
                Value = autoApproveEnabled ? "true" : "false"
            });
            Db.SaveChanges();

            Notifications = new RecordingNotificationService();
            Handler = new PermissionRequestHandler(
                Notifications,
                NullLogger<PermissionRequestHandler>.Instance,
                Microsoft.Extensions.Options.Options.Create(new PermissionRequestHandlerOptions()));

            _service = new ProjectGroupPermissionAutoApprovalService(
                Handler,
                _services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<ProjectGroupPermissionAutoApprovalService>.Instance);
            _service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public AppDbContext Db { get; }

        public PermissionRequestHandler Handler { get; }

        public RecordingNotificationService Notifications { get; }

        public async ValueTask DisposeAsync()
        {
            await _service.StopAsync(CancellationToken.None);
            await Db.DisposeAsync();
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }
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
