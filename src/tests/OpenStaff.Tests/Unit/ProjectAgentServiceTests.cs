using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using OpenStaff.Agent.Services;
using OpenStaff.Application.Agents.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class ProjectAgentServiceTests
{
    /// <summary>
    /// zh-CN: 验证发送消息时会走运行时项目代理通道，并把结果收口成统一对话任务引用。
    /// en: Verifies that sending a message uses the runtime project-agent path and collapses the result into the unified conversation-task output.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_UsesRuntimeProjectAgentPathAndMapsSummary()
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
        var service = new ProjectAgentService(
            new ProjectAgentRoleRepository(db),
            new AgentEventRepository(db),
            agentService,
            new Mock<ILogger<ProjectAgentService>>().Object);

        var response = await service.SendMessageAsync(
            projectId,
            agentId,
            new SendMessageRequest { Content = "hello runtime" },
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

        Assert.Equal(ExecutionPackageStatus.Completed, response.Status);
        Assert.Equal(projectId, response.ProjectId);
        Assert.Equal(SessionSceneTypes.Private, response.Scene);
        Assert.Equal(ExecutionEntryKinds.ProjectAgentPrivate, response.EntryKind);
        Assert.Equal(agentId, response.ProjectAgentRoleId);
        Assert.False(response.IsAwaitingInput);
        Assert.True(agentService.RemoveCalled);
    }

    /// <summary>
    /// zh-CN: 验证未知项目代理会在进入运行时调度前直接失败，避免创建无效消息处理器。
    /// en: Verifies that an unknown project agent fails before runtime dispatch begins, avoiding creation of invalid message handlers.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_UnknownProjectAgent_ThrowsAndSkipsRuntimeDispatch()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateDbContext(connection);

        var projectId = Guid.NewGuid();
        db.Projects.Add(new Project { Id = projectId, Name = "Test project" });
        await db.SaveChangesAsync();

        var agentService = new FakeAgentService(CreateSummary("architect"));
        var service = new ProjectAgentService(
            new ProjectAgentRoleRepository(db),
            new AgentEventRepository(db),
            agentService,
            new Mock<ILogger<ProjectAgentService>>().Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SendMessageAsync(
            projectId,
            Guid.NewGuid(),
            new SendMessageRequest { Content = "hello runtime" },
            CancellationToken.None));

        Assert.Null(agentService.LastRequest);
        Assert.False(agentService.RemoveCalled);
    }

    /// <summary>
    /// zh-CN: 创建使用内存 SQLite 的数据库上下文，让项目代理测试在真实 EF Core 映射上运行。
    /// en: Creates a database context backed by in-memory SQLite so project-agent tests run against real EF Core mappings.
    /// </summary>
    /// <param name="connection">
    /// zh-CN: 供上下文复用的已打开 SQLite 连接。
    /// en: Open SQLite connection reused by the context.
    /// </param>
    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>
    /// zh-CN: 为测试插入项目、角色与项目代理三者关系，确保运行时上下文具备完整导航数据。
    /// en: Seeds the project, role, and project-agent relationship so the runtime context has complete navigation data for the test.
    /// </summary>
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

    /// <summary>
    /// zh-CN: 生成可复用的执行摘要模板，让失败路径测试无需重复搭建完整返回对象。
    /// en: Creates a reusable execution-summary template so failure-path tests do not need to rebuild the full return object every time.
    /// </summary>
    private static MessageExecutionSummary CreateSummary(string roleType)
    {
        return new MessageExecutionSummary(
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
            Usage: null,
            Timing: null,
            ToolCalls: [],
            Error: null);
    }

    /// <summary>
    /// zh-CN: 通过立即完成消息处理器来模拟运行时代理服务，便于验证消息映射和清理逻辑。
    /// en: Simulates the runtime agent service by immediately completing message handlers so message mapping and cleanup logic can be verified.
    /// </summary>
    private sealed class FakeAgentService : IAgentService
    {
        // zh-CN: 通过反射调用 MessageHandler 的内部完成入口，避免在测试中复制运行时完成逻辑。
        // en: Uses reflection to invoke MessageHandler's internal completion entry point so tests do not duplicate runtime completion logic.
        private static readonly MethodInfo CompleteMethod = typeof(MessageHandler)
            .GetMethod("Complete", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MessageHandler.Complete was not found.");
        private readonly MessageExecutionSummary _summary;
        private readonly Dictionary<Guid, MessageHandler> _handlers = new();

        /// <summary>
        /// zh-CN: 保存要回放给调用方的执行摘要模板，供后续创建消息时复用。
        /// en: Stores the execution-summary template that will be replayed to callers when messages are created.
        /// </summary>
        public FakeAgentService(MessageExecutionSummary summary)
        {
            _summary = summary;
        }

        public CreateMessageRequest? LastRequest { get; private set; }

        public bool RemoveCalled { get; private set; }

        /// <summary>
        /// zh-CN: 模拟创建并立即完成一条消息，使被测服务能够按照真实轮询流程读取结果。
        /// en: Simulates creating and immediately completing a message so the service under test can consume results through the normal polling flow.
        /// </summary>
        public Task<CreateMessageResponse> CreateMessageAsync(
            CreateMessageRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;

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
                        Context = request.MessageContext
                    }
                ]);

            return Task.FromResult(new CreateMessageResponse(messageId));
        }

        /// <summary>
        /// zh-CN: 返回已缓存的消息处理器，支持被测服务验证创建后的处理器查询路径。
        /// en: Returns the cached message handler so the service under test can exercise its post-creation handler lookup path.
        /// </summary>
        public bool TryGetMessageHandler(Guid messageId, out MessageHandler? handler)
            => _handlers.TryGetValue(messageId, out handler);

        /// <summary>
        /// zh-CN: 当前测试不覆盖取消流程，因此固定返回 false 以表明未执行取消。
        /// en: These tests do not cover cancellation, so this always returns false to indicate no cancellation work was performed.
        /// </summary>
        public Task<bool> CancelMessageAsync(Guid messageId)
            => Task.FromResult(false);

        /// <summary>
        /// zh-CN: 记录清理动作并释放消息处理器，以验证发送完成后会回收运行时资源。
        /// en: Records cleanup activity and disposes the message handler so the tests can verify runtime resources are released after sending.
        /// </summary>
        public bool RemoveMessageHandler(Guid messageId)
        {
            RemoveCalled = true;

            if (!_handlers.Remove(messageId, out var handler))
                return false;

            handler.Dispose();
            return true;
        }
    }
}

