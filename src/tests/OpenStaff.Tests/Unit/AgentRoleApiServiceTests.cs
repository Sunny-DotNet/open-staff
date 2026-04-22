using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent;
using OpenStaff.AgentSouls.Dtos;
using OpenStaff.AgentSouls.Services;
using OpenStaff.Agent.Services;
using OpenStaff.ApiServices;
using OpenStaff.Application.AgentRoles.Services;
using OpenStaff.Dtos;
using OpenStaff.Application.Sessions.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class AgentRoleApiServiceTests
{
    [Fact]
    public async Task GetAllAsync_WithQueryInput_ReturnsPagedActiveRoles()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        db.AgentRoles.AddRange(
            new AgentRole
            {
                Id = Guid.NewGuid(),
                Name = "Architect",
                JobTitle = "architect",
                IsActive = true
            },
            new AgentRole
            {
                Id = Guid.NewGuid(),
                Name = "Builder",
                JobTitle = "builder",
                IsActive = true
            },
            new AgentRole
            {
                Id = Guid.NewGuid(),
                Name = "Disabled",
                JobTitle = "disabled",
                IsActive = false
            });
        await db.SaveChangesAsync();

        using var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var streamManager = new SessionStreamManager(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ILogger<SessionStreamManager>>());
        var service = new AgentRoleApiService(
            new AgentRoleRepository(db),
            db,
            null!,
            EmptyVendorPlatformCatalog.Instance,
            new FakeAgentService(CreateFailureSummary()),
            streamManager);

        var paged = await service.GetAllAsync(new AgentRoleQueryInput
        {
            Page = 2,
            PageSize = 1
        }, CancellationToken.None);

        Assert.Equal(2, paged.Total);
        var item = Assert.Single(paged.Items);
        Assert.Equal("Builder", item.Name);
    }

    [Fact]
    public async Task TestChatAsync_PushesErrorEvent_WhenRuntimeFails()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        var roleId = Guid.NewGuid();
        db.AgentRoles.Add(new AgentRole
        {
            Id = roleId,
            Name = "Copilot Skill Test UI",
            JobTitle = "copilot_skill_test_ui",
            ProviderType = "github-copilot",
            IsActive = true
        });
        await db.SaveChangesAsync();

        using var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var streamManager = new SessionStreamManager(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ILogger<SessionStreamManager>>());
        var summary = CreateFailureSummary();
        var agentService = new FakeAgentService(summary);
        var service = new AgentRoleApiService(
            new AgentRoleRepository(db),
            db,
            null!,
            EmptyVendorPlatformCatalog.Instance,
            agentService,
            streamManager);

        var result = await service.TestChatAsync(new TestChatRequest
        {
            AgentRoleId = roleId,
            Message = "hello"
        }, CancellationToken.None);

        Assert.True(result.SessionId.HasValue);
        var events = await WaitForEventsAsync(streamManager, result.TaskId);

        Assert.Collection(
            events,
            evt => Assert.Equal(SessionEventTypes.UserInput, evt.EventType),
            evt =>
            {
                Assert.Equal(SessionEventTypes.Error, evt.EventType);
                var payload = JsonDocument.Parse(evt.Payload ?? "{}").RootElement;
                Assert.Equal("Copilot session failed.", payload.GetProperty("error").GetString());
                Assert.Equal("copilot_skill_test_ui", payload.GetProperty("role").GetString());
            });
        Assert.True(agentService.RemoveCalled);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsPersistedAvatar()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        var roleId = Guid.NewGuid();
        db.AgentRoles.Add(new AgentRole
        {
            Id = roleId,
            Name = "Architect",
            JobTitle = "architect",
            IsActive = true
        });
        await db.SaveChangesAsync();

        using var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var streamManager = new SessionStreamManager(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ILogger<SessionStreamManager>>());
        var service = new AgentRoleApiService(
            new AgentRoleRepository(db),
            db,
            null!,
            EmptyVendorPlatformCatalog.Instance,
            new FakeAgentService(CreateFailureSummary()),
            streamManager);

        var result = await service.UpdateAsync(roleId, new UpdateAgentRoleInput
        {
            Avatar = "data:image/png;base64,avatar-test",
            Name = "Architect"
        }, CancellationToken.None);

        Assert.Equal("data:image/png;base64,avatar-test", result.Avatar);
    }

    [Fact]
    public async Task CreateAsync_NormalizesSoulValuesToKeys()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        using var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var streamManager = new SessionStreamManager(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ILogger<SessionStreamManager>>());
        var service = new AgentRoleApiService(
            new AgentRoleRepository(db),
            db,
            null!,
            EmptyVendorPlatformCatalog.Instance,
            new FakeAgentService(CreateFailureSummary()),
            streamManager,
            agentSoulService: new FakeAgentSoulService(
                personalityTraits: new FakeAgentSoulHttpService(
                [
                    new AgentSoulValue("adaptable", new Dictionary<string, string>
                    {
                        ["en"] = "Adaptable",
                        ["zh"] = "适应力强的"
                    })
                ]),
                communicationStyles: new FakeAgentSoulHttpService(
                [
                    new AgentSoulValue("formal", new Dictionary<string, string>
                    {
                        ["en"] = "Formal",
                        ["zh"] = "正式严谨的"
                    })
                ]),
                workAttitudes: new FakeAgentSoulHttpService(
                [
                    new AgentSoulValue("collaborative", new Dictionary<string, string>
                    {
                        ["en"] = "Collaborative",
                        ["zh"] = "注重协作的"
                    })
                ])));

        var result = await service.CreateAsync(new CreateAgentRoleInput
        {
            Name = "Monica",
            RoleType = "monica",
            Soul = new AgentSoulDto
            {
                Traits = ["适应力强的"],
                Style = "正式严谨的",
                Attitudes = ["注重协作的"],
                Custom = "保持专业"
            }
        }, CancellationToken.None);

        var role = await db.AgentRoles.SingleAsync(item => item.Id == result.Id);
        Assert.NotNull(role.Soul);
        Assert.Equal(["adaptable"], role.Soul!.Traits);
        Assert.Equal("formal", role.Soul.Style);
        Assert.Equal(["collaborative"], role.Soul.Attitudes);
        Assert.Equal("保持专业", role.Soul.Custom);
    }

    private static async Task<IReadOnlyList<SessionEvent>> WaitForEventsAsync(
        SessionStreamManager streamManager,
        Guid sessionId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var buffered = streamManager.GetActive(sessionId)?.GetBufferedEvents();
            if (buffered is { Count: >= 2 })
                return buffered.OrderBy(evt => evt.SequenceNo).ToList();

            await Task.Delay(20);
        }

        throw new Xunit.Sdk.XunitException("Timed out waiting for test-chat events.");
    }

    private static MessageExecutionSummary CreateFailureSummary() => new(
        MessageId: Guid.Empty,
        Scene: MessageScene.Test,
        Context: default,
        Success: false,
        Cancelled: false,
        Attempts: 1,
        AgentRole: "copilot_skill_test_ui",
        Model: "claude-haiku-4.5",
        Content: string.Empty,
        Thinking: string.Empty,
        Usage: null,
        Timing: null,
        ToolCalls: [],
        Error: "Copilot session failed.");

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private sealed class EmptyVendorPlatformCatalog : IVendorPlatformCatalog
    {
        public static EmptyVendorPlatformCatalog Instance { get; } = new();

        public IReadOnlyDictionary<string, VendorPlatformServices> Platforms { get; } =
            new Dictionary<string, VendorPlatformServices>(StringComparer.OrdinalIgnoreCase);

        public bool TryGetVendorPlatform(string providerType, out VendorPlatformServices platform)
        {
            platform = default!;
            return false;
        }
    }

    private sealed class FakeAgentService : IAgentService
    {
        private static readonly MethodInfo CompleteMethod = typeof(MessageHandler)
            .GetMethod("Complete", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MessageHandler.Complete was not found.");

        private readonly MessageExecutionSummary _summary;
        private readonly Dictionary<Guid, MessageHandler> _handlers = new();

        public FakeAgentService(MessageExecutionSummary summary)
        {
            _summary = summary;
        }

        public bool RemoveCalled { get; private set; }

        public Task<CreateMessageResponse> CreateMessageAsync(
            CreateMessageRequest request,
            CancellationToken cancellationToken = default)
        {
            var messageId = request.MessageId ?? Guid.NewGuid();
            var handler = new MessageHandler(messageId, request.Scene, request.MessageContext);
            _handlers[messageId] = handler;

            CompleteMethod.Invoke(
                handler,
                [
                    _summary with
                    {
                        MessageId = messageId,
                        Scene = request.Scene,
                        Context = request.MessageContext,
                        AgentRole = request.AgentRoleId?.ToString() ?? _summary.AgentRole,
                    }
                ]);

            return Task.FromResult(new CreateMessageResponse(messageId));
        }

        public bool TryGetMessageHandler(Guid messageId, out MessageHandler? handler)
            => _handlers.TryGetValue(messageId, out handler);

        public Task<bool> CancelMessageAsync(Guid messageId)
            => Task.FromResult(_handlers.ContainsKey(messageId));

        public bool RemoveMessageHandler(Guid messageId)
        {
            RemoveCalled = true;
            return _handlers.Remove(messageId);
        }
    }

    private sealed class FakeAgentSoulService : IAgentSoulService
    {
        public FakeAgentSoulService(
            IAgentSoulHttpService personalityTraits,
            IAgentSoulHttpService communicationStyles,
            IAgentSoulHttpService workAttitudes)
        {
            PersonalityTraits = personalityTraits;
            CommunicationStyles = communicationStyles;
            WorkAttitudes = workAttitudes;
        }

        public IAgentSoulHttpService CommunicationStyles { get; }

        public IAgentSoulHttpService PersonalityTraits { get; }

        public IAgentSoulHttpService WorkAttitudes { get; }
    }

    private sealed class FakeAgentSoulHttpService : IAgentSoulHttpService
    {
        private readonly IReadOnlyCollection<AgentSoulValue> _values;

        public FakeAgentSoulHttpService(IReadOnlyCollection<AgentSoulValue> values)
        {
            _values = values;
        }

        public string DefaultAliasName => "en";

        public Task<IReadOnlyCollection<AgentSoulValue>> GetAllAsync() => Task.FromResult(_values);

        public Task<IReadOnlyDictionary<string, string>> GetAllByLocaleAsync(string? locale = null)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

        public Task<string> GetAsync(string key, string? locale = null)
            => throw new NotSupportedException();
    }
}

