using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Agent.Services;
using OpenStaff.Application.Sessions.Services;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Notifications;
using OpenStaff.Dtos;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Repositories;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class SessionRunnerTests
{
    /// <summary>
    /// zh-CN: 验证项目群里被点名的成员如果返回分发块，SessionRunner 会继续创建并执行后续链路，而不是只把该能力保留给秘书。
    /// en: Verifies that when a targeted ProjectGroup member emits a dispatch envelope, SessionRunner continues the orchestration chain instead of limiting that behavior to the secretary.
    /// </summary>
    [Fact]
    public async Task ExecuteProjectGroupLeaseAsync_WhenMemberReturnsDispatch_ContinuesOrchestration()
    {
        var agentService = new QueueAgentService(
            [
                CreateSummary("我先拆一下。\n\n<openstaff_project_dispatch>{\"dispatches\":[{\"targetRole\":\"architect\",\"task\":\"请补齐接口方案和边界说明\"}]}</openstaff_project_dispatch>"),
                CreateSummary("接口方案已补齐。")
            ]);
        using var context = new TestContext(agentService, TimeSpan.FromMinutes(5));

        var project = await context.AddProjectAsync();
        var producer = await context.AddProjectAgentAsync(project.Id, "Ada", "producer");
        var architect = await context.AddProjectAgentAsync(project.Id, "Bert", "architect");
        var session = await context.AddSessionAsync(project.Id);
        var frame = await context.AddFrameAsync(session.Id, producer.Id, "请先拆一下登录 API");
        await context.AddEntryMessageAsync(session.Id, frame.Id, "@Ada 请先拆一下登录 API");

        var queueResult = await context.ProjectGroupExecution.QueueTaskAsync(
            project.Id,
            session.Id,
            frame.Id,
            new ProjectGroupDispatchTarget(
                "producer",
                "请先拆一下登录 API",
                "@Ada 请先拆一下登录 API",
                true,
                producer.Id,
                "Ada"),
            CancellationToken.None);

        Assert.NotNull(queueResult.Lease);

        await context.ExecuteLeaseAsync(queueResult.Lease!);

        Assert.Equal(2, agentService.Requests.Count);
        Assert.Equal(ChatRole.User, agentService.Requests[0].InputRole);
        Assert.Equal(ChatRole.User, agentService.Requests[1].InputRole);
        Assert.Equal("project_group_mention", agentService.Requests[0].MessageContext.Extra?["openstaff_dispatch_source"]);
        Assert.Equal("project_group_member_dispatch", agentService.Requests[1].MessageContext.Extra?["openstaff_dispatch_source"]);
        Assert.Equal(
            "Another project member finished the current stage and handed the follow-up work to you.",
            agentService.Requests[1].MessageContext.Extra?["openstaff_dispatch_context"]);

        var tasks = await context.Db.Tasks
            .AsNoTracking()
            .OrderBy(item => item.CreatedAt)
            .ToListAsync();
        Assert.Equal(2, tasks.Count);
        Assert.All(tasks, task => Assert.Equal(TaskItemStatus.Done, task.Status));

        var visibleAssistantMessages = await context.Db.ChatMessages
            .AsNoTracking()
            .Where(message => message.SessionId == session.Id
                && message.Role == MessageRoles.Assistant
                && message.ContentType != MessageContentTypes.Internal)
            .OrderBy(message => message.CreatedAt)
            .Select(message => message.Content)
            .ToListAsync();

        Assert.Equal(["我先拆一下。", "接口方案已补齐。"], visibleAssistantMessages);

        var architectTask = tasks.Single(task => task.AssignedProjectAgentRoleId == architect.Id);
        var metadata = TaskItemRuntimeMetadata.TryParse(architectTask.Metadata);
        Assert.NotNull(metadata);
        Assert.Equal("project_group_member_dispatch", metadata!.Source);
    }

    [Fact]
    public async Task ExecuteProjectGroupLeaseAsync_WhenAgentRunTimesOut_ReleasesAgentAndBlocksTask()
    {
        using var context = new TestContext(
            new HangingAgentService(),
            TimeSpan.FromMilliseconds(50));

        var project = await context.AddProjectAsync();
        var producer = await context.AddProjectAgentAsync(project.Id, "Ada", "producer");
        var session = await context.AddSessionAsync(project.Id);
        var frame = await context.AddFrameAsync(session.Id, producer.Id, "请启动浏览器调试");
        await context.AddEntryMessageAsync(session.Id, frame.Id, "@Ada 请启动浏览器调试");

        var queueResult = await context.ProjectGroupExecution.QueueTaskAsync(
            project.Id,
            session.Id,
            frame.Id,
            new ProjectGroupDispatchTarget(
                "producer",
                "请启动浏览器调试",
                "@Ada 请启动浏览器调试",
                true,
                producer.Id,
                "Ada"),
            CancellationToken.None);

        Assert.NotNull(queueResult.Lease);

        await context.ExecuteLeaseAsync(queueResult.Lease!);

        var task = await context.Db.Tasks.AsNoTracking().SingleAsync();
        var updatedAgent = await context.Db.ProjectAgentRoles.AsNoTracking().SingleAsync(a => a.Id == producer.Id);
        var assistantMessages = await context.Db.ChatMessages
            .AsNoTracking()
            .Where(message => message.FrameId == frame.Id && message.Role == MessageRoles.Assistant)
            .OrderBy(message => message.SequenceNo)
            .ToListAsync();

        Assert.Equal(TaskItemStatus.Blocked, task.Status);
        Assert.Equal(AgentStatus.Idle, updatedAgent.Status);
        Assert.Null(updatedAgent.CurrentTask);
        var notice = Assert.Single(assistantMessages);
        Assert.Contains("超时", notice.Content);
    }

    private static MessageExecutionSummary CreateSummary(string content)
        => new(
            Guid.Empty,
            MessageScene.ProjectGroup,
            new MessageContext(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), null, null, null, "producer", "user", Extra: null),
            Success: true,
            Cancelled: false,
            Attempts: 1,
            AgentRole: "producer",
            Model: "fake-model",
            Content: content,
            Thinking: string.Empty,
            Usage: null,
            Timing: null,
            ToolCalls: [],
            Error: null);

    private sealed class TestContext : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;
        private readonly SessionRunner _runner;
        private readonly MethodInfo _executeLeaseMethod;

        public TestContext(IReadOnlyList<MessageExecutionSummary> summaries)
            : this(CreateQueueAgentBundle(summaries), TimeSpan.FromMinutes(5))
        {
        }

        public TestContext(IAgentService agentService, TimeSpan leaseTimeout)
            : this(agentService, agentService as QueueAgentService, leaseTimeout)
        {
        }

        private TestContext(QueueAgentBundle bundle, TimeSpan leaseTimeout)
            : this(bundle.AgentService, bundle.AgentService, leaseTimeout)
        {
        }

        private TestContext(
            IAgentService agentService,
            QueueAgentService? queueAgentService,
            TimeSpan leaseTimeout)
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var services = new ServiceCollection()
                .AddLogging()
                .AddDbContext<AppDbContext>(options => options.UseSqlite(_connection))
                .AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>())
                .AddScoped<IChatSessionRepository>(sp => new ChatSessionRepository(sp.GetRequiredService<AppDbContext>()))
                .AddScoped<IChatFrameRepository>(sp => new ChatFrameRepository(sp.GetRequiredService<AppDbContext>()))
                .AddScoped<IChatMessageRepository>(sp => new ChatMessageRepository(sp.GetRequiredService<AppDbContext>()))
                .AddScoped<ITaskItemRepository>(sp => new TaskItemRepository(sp.GetRequiredService<AppDbContext>()))
                .AddScoped<IAgentEventRepository>(sp => new AgentEventRepository(sp.GetRequiredService<AppDbContext>()))
                .AddScoped<IProjectAgentRoleRepository>(sp => new ProjectAgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
                .AddScoped<IAgentRoleRepository>(sp => new AgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
                .AddScoped<ISessionEventRepository>(sp => new SessionEventRepository(sp.GetRequiredService<AppDbContext>()))
                .AddScoped<IExecutionPackageRepository>(sp => new ExecutionPackageRepository(sp.GetRequiredService<AppDbContext>()))
                .AddScoped<ITaskExecutionLinkRepository>(sp => new TaskExecutionLinkRepository(sp.GetRequiredService<AppDbContext>()));

            _services = services.BuildServiceProvider();
            Db = _services.GetRequiredService<AppDbContext>();
            Db.Database.EnsureCreated();

            AgentService = queueAgentService;
            var sessionStreamManager = new SessionStreamManager(
                _services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<SessionStreamManager>.Instance);
            var mcpToolService = new Mock<IAgentMcpToolService>();
            mcpToolService
                .Setup(service => service.EnsureToolsAllowedAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentMcpCapabilityGrantResult([], [], Changed: false));
            var runtimeCache = new Mock<IProjectAgentRuntimeCache>();

            ProjectGroupExecution = new ProjectGroupExecutionService(
                _services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<ProjectGroupExecutionService>.Instance);
            var capabilityService = new ProjectGroupCapabilityService(
                _services.GetRequiredService<IServiceScopeFactory>(),
                mcpToolService.Object,
                runtimeCache.Object,
                NullLogger<ProjectGroupCapabilityService>.Instance);
            _runner = new SessionRunner(
                sessionStreamManager,
                new NullNotificationService(),
                null!,
                agentService,
                ProjectGroupExecution,
                capabilityService,
                _services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<SessionRunner>.Instance,
                leaseTimeout);
            _executeLeaseMethod = typeof(SessionRunner).GetMethod(
                "ExecuteProjectGroupLeaseAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ExecuteProjectGroupLeaseAsync was not found.");
        }

        public AppDbContext Db { get; }

        public QueueAgentService? AgentService { get; }

        public ProjectGroupExecutionService ProjectGroupExecution { get; }

        public async Task<Project> AddProjectAsync()
        {
            var project = new Project
            {
                Name = "Dispatch Project",
                Language = "zh-CN"
            };
            Db.Projects.Add(project);
            await Db.SaveChangesAsync();
            return project;
        }

        public async Task<ProjectAgentRole> AddProjectAgentAsync(Guid projectId, string name, string roleType)
        {
            var role = new AgentRole
            {
                Name = name,
                JobTitle = roleType,
                IsActive = true
            };
            var projectAgent = new ProjectAgentRole
            {
                ProjectId = projectId,
                AgentRole = role
            };
            Db.ProjectAgentRoles.Add(projectAgent);
            await Db.SaveChangesAsync();
            return projectAgent;
        }

        public async Task<ChatSession> AddSessionAsync(Guid projectId)
        {
            var session = new ChatSession
            {
                ProjectId = projectId,
                Scene = SessionSceneTypes.ProjectGroup,
                Status = SessionStatus.Active,
                InitialInput = "dispatch"
            };
            Db.ChatSessions.Add(session);
            await Db.SaveChangesAsync();
            return session;
        }

        public async Task<ChatFrame> AddFrameAsync(Guid sessionId, Guid targetProjectAgentRoleId, string purpose)
        {
            var frame = new ChatFrame
            {
                SessionId = sessionId,
                Depth = 0,
                Status = FrameStatus.Active,
                Purpose = purpose,
                TargetProjectAgentRoleId = targetProjectAgentRoleId
            };
            Db.ChatFrames.Add(frame);
            await Db.SaveChangesAsync();
            return frame;
        }

        public async Task AddEntryMessageAsync(Guid sessionId, Guid frameId, string content)
        {
            Db.ChatMessages.Add(new OpenStaff.Entities.ChatMessage
            {
                SessionId = sessionId,
                FrameId = frameId,
                Role = MessageRoles.User,
                Content = content,
                SequenceNo = 0
            });
            await Db.SaveChangesAsync();
        }

        public async Task ExecuteLeaseAsync(ProjectGroupExecutionLease lease)
        {
            var task = (Task)_executeLeaseMethod.Invoke(_runner, [lease, CancellationToken.None])!;
            await task;
        }

        public void Dispose()
        {
            Db.Dispose();
            _services.Dispose();
            _connection.Dispose();
        }

        private static QueueAgentBundle CreateQueueAgentBundle(IReadOnlyList<MessageExecutionSummary> summaries)
            => new(new QueueAgentService(summaries));

        private sealed record QueueAgentBundle(QueueAgentService AgentService);
    }

    private sealed class QueueAgentService : IAgentService
    {
        private static readonly MethodInfo CompleteMethod = typeof(MessageHandler).GetMethod(
            "Complete",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MessageHandler.Complete was not found.");

        private readonly ConcurrentDictionary<Guid, MessageHandler> _handlers = new();
        private readonly Queue<MessageExecutionSummary> _summaries;

        public QueueAgentService(IReadOnlyList<MessageExecutionSummary> summaries)
        {
            _summaries = new Queue<MessageExecutionSummary>(summaries);
        }

        public List<CreateMessageRequest> Requests { get; } = [];

        public Task<CreateMessageResponse> CreateMessageAsync(CreateMessageRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var messageId = request.MessageId ?? Guid.NewGuid();
            var handler = new MessageHandler(messageId, request.Scene, request.MessageContext);
            _handlers[messageId] = handler;

            var summary = _summaries.Count > 0
                ? _summaries.Dequeue()
                : CreateSummary("ok");
            CompleteMethod.Invoke(
                handler,
                [
                    summary with
                    {
                        MessageId = messageId,
                        Scene = request.Scene,
                        Context = request.MessageContext
                    }
                ]);

            return Task.FromResult(new CreateMessageResponse(messageId));
        }

        public bool TryGetMessageHandler(Guid messageId, out MessageHandler? handler)
            => _handlers.TryGetValue(messageId, out handler);

        public Task<bool> CancelMessageAsync(Guid messageId)
            => Task.FromResult(false);

        public bool RemoveMessageHandler(Guid messageId)
        {
            if (!_handlers.Remove(messageId, out var handler))
                return false;

            handler.Dispose();
            return true;
        }
    }

    private sealed class HangingAgentService : IAgentService
    {
        private static readonly MethodInfo CompleteMethod = typeof(MessageHandler).GetMethod(
            "Complete",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MessageHandler.Complete was not found.");

        private readonly ConcurrentDictionary<Guid, MessageHandler> _handlers = new();

        public Task<CreateMessageResponse> CreateMessageAsync(CreateMessageRequest request, CancellationToken cancellationToken = default)
        {
            var messageId = request.MessageId ?? Guid.NewGuid();
            var handler = new MessageHandler(messageId, request.Scene, request.MessageContext);
            _handlers[messageId] = handler;

            cancellationToken.Register(() =>
            {
                CompleteMethod.Invoke(
                    handler,
                    [
                        new MessageExecutionSummary(
                            messageId,
                            request.Scene,
                            request.MessageContext,
                            Success: false,
                            Cancelled: true,
                            Attempts: 1,
                            AgentRole: request.MessageContext.TargetRole,
                            Model: "fake-model",
                            Content: string.Empty,
                            Thinking: string.Empty,
                            Usage: null,
                            Timing: null,
                            ToolCalls: [],
                            Error: "Message execution cancelled.")
                    ]);
            });

            return Task.FromResult(new CreateMessageResponse(messageId));
        }

        public bool TryGetMessageHandler(Guid messageId, out MessageHandler? handler)
            => _handlers.TryGetValue(messageId, out handler);

        public Task<bool> CancelMessageAsync(Guid messageId)
            => Task.FromResult(false);

        public bool RemoveMessageHandler(Guid messageId)
        {
            if (!_handlers.Remove(messageId, out var handler))
                return false;

            handler.Dispose();
            return true;
        }
    }

    private sealed class NullNotificationService : INotificationService
    {
        public Task NotifyAsync(string channel, string eventType, object? payload = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task PublishSessionEventAsync(Guid sessionId, SessionEvent sessionEvent, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
