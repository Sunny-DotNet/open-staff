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
    /// zh-CN: 验证成员如果试图继续分发后续工作，SessionRunner 会把协作请求回流给项目编排内核，再由秘书外壳继续派发，而不是直接成员对成员串联。
    /// en: Verifies that when a member tries to dispatch follow-up work, SessionRunner relays the collaboration request back to the project orchestrator so the secretary facade can continue dispatching instead of allowing direct member-to-member chaining.
    /// </summary>
    [Fact]
    public async Task ExecuteProjectGroupLeaseAsync_WhenMemberReturnsDispatch_RelaysBackToProjectOrchestrator()
    {
        var agentService = new QueueAgentService(
            [
                CreateSummary("我先拆一下。\n\n<openstaff_project_dispatch>{\"dispatches\":[{\"targetRole\":\"architect\",\"task\":\"请补齐接口方案和边界说明\"}]}</openstaff_project_dispatch>"),
                CreateSummary("{\"replyMode\":\"secretary_reply_and_dispatch\",\"secretaryReply\":\"收到，我来继续安排。\",\"dispatches\":[{\"targetRole\":\"architect\",\"task\":\"请补齐接口方案和边界说明\"}]}"),
                CreateSummary("接口方案已补齐。")
            ]);
        using var context = new TestContext(agentService, TimeSpan.FromMinutes(5));

        var project = await context.AddProjectAsync();
        var producer = await context.AddProjectAgentAsync(project.Id, "Producer", "producer");
        var architect = await context.AddProjectAgentAsync(project.Id, "Architect", "architect");
        var session = await context.AddSessionAsync(project.Id, scene: SessionSceneTypes.ProjectGroup);
        var frame = await context.AddFrameAsync(session.Id, producer.Id, "请先拆一下登录 API");
        await context.AddEntryMessageAsync(session.Id, frame.Id, "@Producer 请先拆一下登录 API");

        var queueResult = await context.ProjectGroupExecution.QueueTaskAsync(
            project.Id,
            session.Id,
            frame.Id,
            new ProjectGroupDispatchTarget(
                "producer",
                "请先拆一下登录 API",
                "@Producer 请先拆一下登录 API",
                true,
                producer.Id,
                "Producer"),
            CancellationToken.None);

        Assert.NotNull(queueResult.Lease);

        await context.ExecuteLeaseAsync(queueResult.Lease!);

        Assert.Equal(3, agentService.Requests.Count);
        Assert.Equal(ChatRole.User, agentService.Requests[0].InputRole);
        Assert.Equal(ChatRole.User, agentService.Requests[1].InputRole);
        Assert.Equal(ChatRole.User, agentService.Requests[2].InputRole);
        var dispatchSources = agentService.Requests
            .Select(request => request.MessageContext.Extra?["openstaff_dispatch_source"])
            .ToArray();
        Assert.Equal(
            ["project_group_mention", "project_group_member_replan", "project_group_secretary_dispatch"],
            dispatchSources);
        Assert.Equal(
            "Another project member reported progress and asked the hidden project orchestrator to re-plan the next collaboration step and choose who should speak next.",
            agentService.Requests[1].MessageContext.Extra?["openstaff_dispatch_context"]);

        context.Db.ChangeTracker.Clear();
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

        Assert.Equal(["我先拆一下。", "收到，我来继续安排。", "接口方案已补齐。"], visibleAssistantMessages);

        var architectTask = tasks.Single(task => task.AssignedProjectAgentRoleId == architect.Id);
        var metadata = TaskItemRuntimeMetadata.TryParse(architectTask.Metadata);
        Assert.NotNull(metadata);
        Assert.Equal("project_group_secretary_dispatch", metadata!.Source);
    }

    /// <summary>
    /// zh-CN: 验证隐藏项目模型若只输出分派包而不输出可见文案时，群聊里不会先落一条空的秘书消息，而是直接显示被选中成员的回复。
    /// en: Verifies that when the hidden project orchestrator emits only a dispatch block, the group chat skips any empty secretary message and shows only the selected member reply.
    /// </summary>
    [Fact]
    public async Task ExecuteFrameAsync_WhenOrchestratorDispatchesWithoutVisibleText_SkipsSecretaryFacadeMessage()
    {
        var agentService = new QueueAgentService(
            [
                CreateSummary("{\"replyMode\":\"dispatch_only\",\"dispatches\":[{\"targetRole\":\"producer\",\"task\":\"直接回复：backend dispatch ok\"}]}"),
                CreateSummary("backend dispatch ok")
            ]);
        using var context = new TestContext(agentService, TimeSpan.FromMinutes(5));

        var project = await context.AddProjectAsync();
        var producer = await context.AddProjectAgentAsync(project.Id, "Sophie", "producer");
        var session = await context.AddSessionAsync(project.Id, scene: SessionSceneTypes.ProjectGroup);
        var frame = await context.AddOrchestratorFrameAsync(session.Id, "Sophie 回复“backend dispatch ok”", "Sophie 回复“backend dispatch ok”");

        await context.ExecuteFrameAsync(session, frame, SceneType.ProjectGroup);

        Assert.Equal(2, agentService.Requests.Count);
        Assert.Equal(BuiltinRoleTypes.Secretary, agentService.Requests[0].MessageContext.TargetRole);
        Assert.Equal("producer", agentService.Requests[1].MessageContext.TargetRole);
        Assert.Equal("project_group_secretary_dispatch", agentService.Requests[1].MessageContext.Extra?["openstaff_dispatch_source"]);

        var visibleAssistantMessages = await context.Db.ChatMessages
            .AsNoTracking()
            .Where(message => message.SessionId == session.Id
                && message.Role == MessageRoles.Assistant
                && message.ContentType != MessageContentTypes.Internal)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync();

        var visibleMessage = Assert.Single(visibleAssistantMessages);
        Assert.Equal("backend dispatch ok", visibleMessage.Content);
        Assert.Equal(producer.Id, visibleMessage.ProjectAgentRoleId);
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
        private readonly MethodInfo _executeFrameMethod;
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
            _executeFrameMethod = typeof(SessionRunner).GetMethod(
                "ExecuteFrameAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ExecuteFrameAsync was not found.");
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
            Db.AgentRoles.Add(role);
            await Db.SaveChangesAsync();

            var projectAgent = new ProjectAgentRole
            {
                ProjectId = projectId,
                AgentRoleId = role.Id,
                AgentRole = role
            };
            Db.ProjectAgentRoles.Add(projectAgent);
            await Db.SaveChangesAsync();
            return projectAgent;
        }

        public async Task<ChatSession> AddSessionAsync(Guid projectId, string scene = SessionSceneTypes.ProjectGroup)
        {
            var session = new ChatSession
            {
                ProjectId = projectId,
                Scene = scene,
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

        public async Task<ChatFrame> AddOrchestratorFrameAsync(Guid sessionId, string purpose, string input)
        {
            var secretaryRole = await Db.AgentRoles.FirstOrDefaultAsync(role =>
                role.IsBuiltin
                && role.IsActive
                && role.JobTitle == BuiltinRoleTypes.Secretary);
            if (secretaryRole == null)
            {
                secretaryRole = new AgentRole
                {
                    IsActive = true,
                    IsBuiltin = true,
                    JobTitle = BuiltinRoleTypes.Secretary,
                    Name = "Monica"
                };
                Db.AgentRoles.Add(secretaryRole);
                await Db.SaveChangesAsync();
            }

            var frame = new ChatFrame
            {
                SessionId = sessionId,
                Depth = 0,
                Status = FrameStatus.Active,
                Purpose = purpose,
                TargetAgentRoleId = secretaryRole.Id
            };
            Db.ChatFrames.Add(frame);
            Db.ChatMessages.Add(new OpenStaff.Entities.ChatMessage
            {
                SessionId = sessionId,
                FrameId = frame.Id,
                Role = MessageRoles.User,
                Content = input,
                SequenceNo = 0
            });
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

        public async Task ExecuteFrameAsync(ChatSession session, ChatFrame frame, SceneType scene)
        {
            var task = (Task)_executeFrameMethod.Invoke(_runner, [session, frame, scene, CancellationToken.None, null, null, null])!;
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
