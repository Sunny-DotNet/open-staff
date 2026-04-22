using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenStaff.Agent.Services;
using OpenStaff.Application.Conversations.Models;
using OpenStaff.Application.Conversations.Services;
using OpenStaff.Application.Sessions.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class ConversationEntryServiceTests
{
    /// <summary>
    /// zh-CN: 验证统一入口服务处理测试对话时，会沿用瞬态事件流并把运行时失败整理成前端可消费的错误事件。
    /// 这个用例存在的原因，是为了确保测试对话已经真正穿过统一入口服务，而不是还偷偷走 AgentRoleApiService 里的旧私有实现。
    /// en: Verifies the unified entry service still emits transient test-chat events and shapes runtime failures into frontend-consumable error events.
    /// </summary>
    [Fact]
    public async Task StartTestChatAsync_PushesErrorEvent_WhenRuntimeFails()
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
        var taskStreamManager = new TaskStreamManager(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ILogger<TaskStreamManager>>());

        var service = new ConversationEntryService(
            new AgentRoleRepository(db),
            new ProjectAgentRoleRepository(db),
            new FakeAgentService(CreateFailureSummary()),
            streamManager,
            taskStreamManager,
            new Mock<ILogger<ConversationEntryService>>().Object);

        var result = await service.StartTestChatAsync(
            new TestChatEntry(roleId, "hello"),
            CancellationToken.None);

        Assert.True(result.SessionId.HasValue);
        var events = await WaitForEventsAsync(taskStreamManager, result.TaskId);

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
    }

    /// <summary>
    /// zh-CN: 验证统一入口服务处理项目成员私聊时，会统一走项目代理目标链路，并把执行摘要映射回原有 API 响应结构。
    /// 这样可以保证“先统一入口、后逐步重构底层”的策略不会把前端返回契约一并打碎。
    /// en: Verifies the unified entry service uses the project-agent execution path and preserves the existing API response shape.
    /// </summary>
    [Fact]
    public async Task SendProjectAgentPrivateAsync_UsesRuntimeProjectAgentPathAndMapsSummary()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateDbContext(connection);

        var projectId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        const string roleType = "architect";
        SeedProjectAgent(db, projectId, agentId, roleType);

        var summary = new MessageExecutionSummary(
            MessageId: Guid.Empty,
            Scene: MessageScene.Private,
            Context: default,
            Success: true,
            Cancelled: false,
            Attempts: 1,
            AgentRole: roleType,
            Model: "gpt-test",
            Content: "runtime response",
            Thinking: string.Empty,
            Usage: new MessageUsageSnapshot(12, 34, 46),
            Timing: new MessageTimingSnapshot(250, 80),
            ToolCalls: [],
            Error: null);

        var agentService = new FakeAgentService(summary);
        var streamManager = CreateStreamManager();
        var taskStreamManager = CreateTaskStreamManager();
        var service = new ConversationEntryService(
            new AgentRoleRepository(db),
            new ProjectAgentRoleRepository(db),
            agentService,
            streamManager,
            taskStreamManager,
            new Mock<ILogger<ConversationEntryService>>().Object);

        var response = await service.SendProjectAgentPrivateAsync(
            new ProjectAgentPrivateEntry(projectId, agentId, "hello runtime"),
            CancellationToken.None);

        Assert.NotNull(agentService.LastRequest);
        var request = agentService.LastRequest!.Value;
        Assert.Equal(MessageScene.Private, request.Scene);
        Assert.Equal(projectId, request.MessageContext.ProjectId);
        Assert.Equal(agentId, request.MessageContext.ProjectAgentRoleId);
        Assert.Equal(roleType, request.MessageContext.TargetRole);
        Assert.Equal(MessageRoles.User, request.MessageContext.InitiatorRole);
        Assert.Equal(ChatRole.User, request.InputRole);
        Assert.Equal("hello runtime", request.Input);

        Assert.Equal(projectId, response.ProjectId);
        Assert.True(response.SessionId.HasValue);
        Assert.Equal(SessionSceneTypes.Private, response.Scene);
        Assert.Equal(ExecutionEntryKinds.ProjectAgentPrivate, response.EntryKind);
        Assert.Equal(agentId, response.ProjectAgentRoleId);
        Assert.Equal(ExecutionPackageStatus.Active, response.Status);
        for (var attempt = 0; attempt < 20 && !agentService.RemoveCalled; attempt++)
        {
            await Task.Delay(25);
        }
        Assert.True(agentService.RemoveCalled);
    }

    /// <summary>
    /// zh-CN: 创建使用内存 SQLite 的数据库上下文，让统一入口服务测试跑在真实 EF 映射上。
    /// en: Creates an AppDbContext backed by in-memory SQLite so unified-entry tests run against the real EF model.
    /// </summary>
    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static SessionStreamManager CreateStreamManager()
    {
        using var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        return new SessionStreamManager(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ILogger<SessionStreamManager>>());
    }

    private static TaskStreamManager CreateTaskStreamManager()
    {
        using var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        return new TaskStreamManager(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ILogger<TaskStreamManager>>());
    }

    private static void SeedProjectAgent(AppDbContext db, Guid projectId, Guid agentId, string roleType)
    {
        var roleId = Guid.NewGuid();

        db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Test project"
        });
        db.AgentRoles.Add(new AgentRole
        {
            Id = roleId,
            Name = "Architect",
            JobTitle = roleType,
            IsActive = true
        });
        db.ProjectAgentRoles.Add(new ProjectAgentRole
        {
            Id = agentId,
            ProjectId = projectId,
            AgentRoleId = roleId
        });

        db.SaveChanges();
    }

    private static async Task<IReadOnlyList<SessionEvent>> WaitForEventsAsync(
        TaskStreamManager taskStreamManager,
        Guid taskId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var events = new List<SessionEvent>();

        await foreach (var evt in taskStreamManager.SubscribeAsync(taskId, cts.Token))
        {
            events.Add(evt);
            if (events.Count >= 2)
                return events.OrderBy(item => item.SequenceNo).ToList();
        }

        throw new Xunit.Sdk.XunitException("Timed out waiting for conversation-entry events.");
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

    /// <summary>
    /// zh-CN: 通过立即完成消息处理器来模拟运行时代理服务，便于验证统一入口层的映射和收尾行为。
    /// en: Simulates the runtime agent service by immediately completing message handlers so the unified-entry layer can be tested in isolation.
    /// </summary>
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

        public CreateMessageRequest? LastRequest { get; private set; }

        public bool RemoveCalled { get; private set; }

        public Task<CreateMessageResponse> CreateMessageAsync(CreateMessageRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var messageId = Guid.NewGuid();
            var handler = new MessageHandler(messageId, request.Scene, request.MessageContext);
            _handlers[messageId] = handler;

            var summary = _summary with
            {
                MessageId = messageId,
                Scene = request.Scene,
                Context = request.MessageContext
            };

            CompleteMethod.Invoke(handler, [summary]);
            return Task.FromResult(new CreateMessageResponse(messageId));
        }

        public bool TryGetMessageHandler(Guid messageId, out MessageHandler? handler)
            => _handlers.TryGetValue(messageId, out handler);

        public Task<bool> CancelMessageAsync(Guid messageId) => Task.FromResult(false);

        public bool RemoveMessageHandler(Guid messageId)
        {
            RemoveCalled = true;
            return _handlers.Remove(messageId);
        }

        public void Dispose()
        {
        }
    }
}
