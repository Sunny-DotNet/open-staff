using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;
using OpenStaff.Application.Sessions.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class SessionApiServiceTests
{
    /// <summary>
    /// zh-CN: 验证按场景获取当前会话时，会忽略其他场景并返回同场景下最新的可交互会话。
    /// en: Verifies active-session lookup ignores other scenes and returns the newest interactive session for the requested scene.
    /// </summary>
    [Fact]
    public async Task GetActiveBySceneAsync_ReturnsLatestActiveSessionForScene()
    {
        using var context = new TestContext();
        var projectId = Guid.NewGuid();
        await context.AddProjectAsync(projectId, "Scene Filter");
        var olderGroup = new ChatSession
        {
            ProjectId = projectId,
            Scene = SessionSceneTypes.ProjectGroup,
            Status = SessionStatus.Active,
            InitialInput = "old-group",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        var latestGroup = new ChatSession
        {
            ProjectId = projectId,
            Scene = SessionSceneTypes.ProjectGroup,
            Status = SessionStatus.AwaitingInput,
            InitialInput = "latest-group",
            CreatedAt = DateTime.UtcNow
        };
        var brainstorm = new ChatSession
        {
            ProjectId = projectId,
            Scene = SessionSceneTypes.ProjectBrainstorm,
            Status = SessionStatus.Active,
            InitialInput = "brainstorm",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        context.Db.ChatSessions.AddRange(olderGroup, latestGroup, brainstorm);
        await context.Db.SaveChangesAsync();

        var result = await context.Service.GetActiveBySceneAsync(new GetActiveProjectSessionRequest
        {
            ProjectId = projectId,
            Scene = "projectgroup"
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(latestGroup.Id, result!.Id);
        Assert.Equal(SessionSceneTypes.ProjectGroup, result.Scene);
    }

    /// <summary>
    /// zh-CN: 验证按项目列出会话时，场景筛选只返回目标项目中匹配场景的记录。
    /// en: Verifies project session queries honor the optional scene filter and exclude sessions from other projects.
    /// </summary>
    [Fact]
    public async Task GetByProjectAsync_FiltersByScene_WhenProvided()
    {
        using var context = new TestContext();
        var projectId = Guid.NewGuid();
        await context.AddProjectAsync(projectId, "Project Sessions");

        context.Db.ChatSessions.AddRange(
            new ChatSession
            {
                ProjectId = projectId,
                Scene = SessionSceneTypes.ProjectBrainstorm,
                Status = SessionStatus.Completed,
                InitialInput = "brainstorm"
            },
            new ChatSession
            {
                ProjectId = projectId,
                Scene = SessionSceneTypes.ProjectGroup,
                Status = SessionStatus.Active,
                InitialInput = "group"
            },
            new ChatSession
            {
                ProjectId = await context.AddProjectAsync(Guid.NewGuid(), "Other Project"),
                Scene = SessionSceneTypes.ProjectBrainstorm,
                Status = SessionStatus.Active,
                InitialInput = "other-project"
            });
        await context.Db.SaveChangesAsync();

        var result = await context.Service.GetByProjectAsync(new GetSessionsByProjectRequest
        {
            ProjectId = projectId,
            Scene = SessionSceneTypes.ProjectBrainstorm,
            Limit = 10
        }, CancellationToken.None);

        var session = Assert.Single(result);
        Assert.Equal(SessionSceneTypes.ProjectBrainstorm, session.Scene);
        Assert.Equal(projectId, session.ProjectId);
    }

    /// <summary>
    /// zh-CN: 验证项目仍处于头脑风暴阶段时，服务会阻止提前进入项目群聊场景。
    /// en: Verifies the service blocks entry into the project-group scene while the project is still in brainstorming.
    /// </summary>
    [Fact]
    public async Task CreateAsync_BlocksProjectGroupBeforeProjectStart()
    {
        using var context = new TestContext();
        var projectId = Guid.NewGuid();
        await context.AddProjectAsync(projectId, "Group Guard", ProjectPhases.Brainstorming);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.CreateAsync(new CreateSessionInput
        {
            ProjectId = projectId,
            Input = "进入群聊",
            Scene = SessionSceneTypes.ProjectGroup
        }, CancellationToken.None));

        Assert.Contains("项目尚未启动", ex.Message);
    }

    /// <summary>
    /// zh-CN: 验证项目已经启动后，需求讨论场景会切换为只读，防止继续修改脑暴会话。
    /// en: Verifies brainstorming sessions become read-only after the project starts so requirement discovery is not modified mid-execution.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_BlocksBrainstormSessionAfterProjectStarted()
    {
        using var context = new TestContext();
        var projectId = Guid.NewGuid();
        await context.AddProjectAsync(projectId, "Brainstorm Guard", ProjectPhases.Running);

        var session = new ChatSession
        {
            ProjectId = projectId,
            Scene = SessionSceneTypes.ProjectBrainstorm,
            Status = SessionStatus.Active,
            InitialInput = "brainstorm"
        };
        context.Db.ChatSessions.Add(session);
        await context.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.SendMessageAsync(new SendSessionMessageRequest
        {
            SessionId = session.Id,
            Input = "继续补充需求"
        }, CancellationToken.None));

        Assert.Contains("当前只读", ex.Message);
    }

    /// <summary>
    /// zh-CN: 验证聊天消息查询会过滤内部内容类型，只向前端返回用户可见消息。
    /// en: Verifies chat message retrieval filters out internal content so only user-visible messages are returned to the client.
    /// </summary>
    [Fact]
    public async Task GetChatMessagesAsync_FiltersInternalMessages()
    {
        using var context = new TestContext();
        var projectId = Guid.NewGuid();
        await context.AddProjectAsync(projectId, "Internal Filter");

        var session = new ChatSession
        {
            ProjectId = projectId,
            Scene = SessionSceneTypes.ProjectGroup,
            Status = SessionStatus.Active,
            InitialInput = "session"
        };
        context.Db.ChatSessions.Add(session);

        var frame = new ChatFrame
        {
            SessionId = session.Id,
            Depth = 0,
            Purpose = "安排任务",
            Status = FrameStatus.Active
        };
        context.Db.ChatFrames.Add(frame);
        context.Db.ChatMessages.AddRange(
            new ChatMessage
            {
                SessionId = session.Id,
                FrameId = frame.Id,
                Role = MessageRoles.User,
                Content = "可见消息",
                ContentType = MessageContentTypes.Text,
                SequenceNo = 0
            },
            new ChatMessage
            {
                SessionId = session.Id,
                FrameId = frame.Id,
                Role = MessageRoles.Assistant,
                Content = "内部调度消息",
                ContentType = MessageContentTypes.Internal,
                SequenceNo = 1
            });
        await context.Db.SaveChangesAsync();

        var result = await context.Service.GetChatMessagesAsync(new GetChatMessagesRequest
        {
            SessionId = session.Id,
            Skip = 0,
            Take = 20
        }, CancellationToken.None);

        var message = Assert.Single(result.Messages);
        Assert.Equal("可见消息", message.Content);
        Assert.Equal(1, result.Total);
    }

    /// <summary>
    /// zh-CN: 验证消息查询会把令牌消耗与耗时 JSON 正确映射为结构化元数据。
    /// en: Verifies message queries map token-usage and timing JSON into structured metadata for the caller.
    /// </summary>
    [Fact]
    public async Task GetChatMessagesAsync_MapsUsageAndTimingMetadata()
    {
        using var context = new TestContext();
        var projectId = Guid.NewGuid();
        await context.AddProjectAsync(projectId, "Usage Mapping");

        var session = new ChatSession
        {
            ProjectId = projectId,
            Scene = SessionSceneTypes.ProjectBrainstorm,
            Status = SessionStatus.Active,
            InitialInput = "session"
        };
        context.Db.ChatSessions.Add(session);

        var frame = new ChatFrame
        {
            SessionId = session.Id,
            Depth = 0,
            Purpose = "收集需求",
            Status = FrameStatus.Active
        };
        context.Db.ChatFrames.Add(frame);
        context.Db.ChatMessages.Add(new ChatMessage
        {
            SessionId = session.Id,
            FrameId = frame.Id,
            Role = MessageRoles.Assistant,
            Content = "已整理好需求。",
            ContentType = MessageContentTypes.Text,
            SequenceNo = 0,
            TokenUsage = """{"inputTokens":12,"outputTokens":34,"totalTokens":46}""",
            DurationMs = 2300
        });
        await context.Db.SaveChangesAsync();

        var result = await context.Service.GetChatMessagesAsync(new GetChatMessagesRequest
        {
            SessionId = session.Id,
            Skip = 0,
            Take = 20
        }, CancellationToken.None);

        var message = Assert.Single(result.Messages);
        Assert.Equal(46, message.TokenUsage);
        Assert.NotNull(message.Usage);
        Assert.Equal(12, message.Usage!.InputTokens);
        Assert.Equal(34, message.Usage.OutputTokens);
        Assert.Equal(46, message.Usage.TotalTokens);
        Assert.NotNull(message.Timing);
        Assert.Equal(2300, message.Timing!.TotalMs);
        Assert.Equal(2300, message.DurationMs);
    }

    /// <summary>
    /// zh-CN: 验证会话仍处于活动状态且存在内存流时，事件查询优先回放缓冲区中的实时事件。
    /// en: Verifies event retrieval prefers buffered real-time events from the stream manager while the session is still active.
    /// </summary>
    [Fact]
    public async Task GetEventsAsync_ReturnsBufferedEventsForActiveSession()
    {
        using var context = new TestContext();
        var projectId = Guid.NewGuid();
        await context.AddProjectAsync(projectId, "Active Event Replay");

        var session = new ChatSession
        {
            ProjectId = projectId,
            Scene = SessionSceneTypes.ProjectBrainstorm,
            Status = SessionStatus.Active,
            InitialInput = "session"
        };
        context.Db.ChatSessions.Add(session);
        await context.Db.SaveChangesAsync();

        var stream = context.StreamManager.Create(session.Id);
        var frameId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        stream.Push(new SessionEvent
        {
            EventType = "message",
            FrameId = frameId,
            MessageId = messageId,
            Payload = """{"content":"active event"}""",
            CreatedAt = DateTime.UtcNow
        });

        var result = await context.Service.GetEventsAsync(session.Id, CancellationToken.None);

        var evt = Assert.Single(result);
        Assert.Equal("message", evt.EventType);
        Assert.Equal(frameId, evt.FrameId);
        Assert.Equal(messageId, evt.MessageId);
        Assert.Equal(1, evt.SequenceNo);
    }

    /// <summary>
    /// zh-CN: 验证按会话详情查询时，会正确映射父子 Frame 关系以及入口消息上下文。
    /// en: Verifies session-detail retrieval correctly maps parent-child frame relationships and entry-message context.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_MapsFrameHierarchyAndEntryMessageContext()
    {
        using var context = new TestContext();
        var projectId = Guid.NewGuid();
        await context.AddProjectAsync(projectId, "Frame Mapping");

        var session = new ChatSession
        {
            ProjectId = projectId,
            Scene = SessionSceneTypes.ProjectGroup,
            Status = SessionStatus.Active,
            InitialInput = "session"
        };
        context.Db.ChatSessions.Add(session);

        var taskId = Guid.NewGuid();
        context.Db.Tasks.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            Title = "处理任务"
        });

        var parentFrame = new ChatFrame
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Depth = 0,
            Purpose = "安排任务",
            Status = FrameStatus.Completed,
            Result = "已安排",
            CompletedAt = DateTime.UtcNow
        };
        var childFrame = new ChatFrame
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ParentFrameId = parentFrame.Id,
            TaskId = taskId,
            Depth = 1,
            Purpose = "编写 API",
            Status = FrameStatus.Active
        };
        context.Db.ChatFrames.AddRange(parentFrame, childFrame);

        var parentEntryMessageId = Guid.NewGuid();
        var childEntryMessageId = Guid.NewGuid();
        context.Db.ChatMessages.AddRange(
            new ChatMessage
            {
                Id = parentEntryMessageId,
                SessionId = session.Id,
                FrameId = parentFrame.Id,
                Role = MessageRoles.User,
                Content = "安排任务",
                SequenceNo = 0
            },
            new ChatMessage
            {
                Id = childEntryMessageId,
                SessionId = session.Id,
                FrameId = childFrame.Id,
                ParentMessageId = parentEntryMessageId,
                Role = MessageRoles.Assistant,
                Content = "请 producer 编写 API",
                ContentType = MessageContentTypes.Internal,
                SequenceNo = 0
            });
        await context.Db.SaveChangesAsync();

        var result = await context.Service.GetByIdAsync(session.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result!.Frames);
        Assert.Equal(2, result.Frames!.Count);

        var mappedParent = result.Frames.Single(frame => frame.Id == parentFrame.Id);
        Assert.Null(mappedParent.ParentFrameId);
        Assert.Equal(parentEntryMessageId, mappedParent.EntryMessageId);
        Assert.Equal("安排任务", mappedParent.Purpose);
        Assert.Equal("已安排", mappedParent.Result);

        var mappedChild = result.Frames.Single(frame => frame.Id == childFrame.Id);
        Assert.Equal(parentFrame.Id, mappedChild.ParentFrameId);
        Assert.Equal(taskId, mappedChild.TaskId);
        Assert.Equal(childEntryMessageId, mappedChild.EntryMessageId);
        Assert.Equal(parentEntryMessageId, mappedChild.ParentMessageId);
        Assert.Equal(1, mappedChild.Depth);
    }

    /// <summary>
    /// zh-CN: 验证实时流不可用时，事件查询会按顺序回退到数据库中的持久化事件。
    /// en: Verifies event retrieval falls back to persisted database events in sequence when no active stream is available.
    /// </summary>
    [Fact]
    public async Task GetEventsAsync_WhenStreamInactive_ReturnsPersistedEventsInSequence()
    {
        using var context = new TestContext();
        var projectId = Guid.NewGuid();
        await context.AddProjectAsync(projectId, "Persisted Event Replay");

        var session = new ChatSession
        {
            ProjectId = projectId,
            Scene = SessionSceneTypes.ProjectGroup,
            Status = SessionStatus.Completed,
            InitialInput = "session"
        };
        context.Db.ChatSessions.Add(session);
        await context.Db.SaveChangesAsync();

        var frameId = Guid.NewGuid();
        var firstMessageId = Guid.NewGuid();
        var secondMessageId = Guid.NewGuid();
        context.Db.ChatFrames.Add(new ChatFrame
        {
            Id = frameId,
            SessionId = session.Id,
            Depth = 0,
            Purpose = "回放事件",
            Status = FrameStatus.Completed
        });
        context.Db.ChatMessages.AddRange(
            new ChatMessage
            {
                Id = firstMessageId,
                SessionId = session.Id,
                FrameId = frameId,
                Role = MessageRoles.Assistant,
                Content = "persisted message",
                SequenceNo = 0
            },
            new ChatMessage
            {
                Id = secondMessageId,
                SessionId = session.Id,
                FrameId = frameId,
                ParentMessageId = firstMessageId,
                Role = MessageRoles.Assistant,
                Content = "tool host",
                SequenceNo = 1
            });
        context.Db.SessionEvents.AddRange(
            new SessionEvent
            {
                SessionId = session.Id,
                FrameId = frameId,
                MessageId = secondMessageId,
                EventType = SessionEventTypes.ToolCall,
                Payload = """{"name":"search"}""",
                SequenceNo = 2
            },
            new SessionEvent
            {
                SessionId = session.Id,
                FrameId = frameId,
                MessageId = firstMessageId,
                EventType = SessionEventTypes.Message,
                Payload = """{"content":"persisted message"}""",
                SequenceNo = 1
            });
        await context.Db.SaveChangesAsync();

        var result = await context.Service.GetEventsAsync(session.Id, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(SessionEventTypes.Message, result[0].EventType);
        Assert.Equal(firstMessageId, result[0].MessageId);
        Assert.Equal(1, result[0].SequenceNo);
        Assert.Equal(SessionEventTypes.ToolCall, result[1].EventType);
        Assert.Equal(secondMessageId, result[1].MessageId);
        Assert.Equal(2, result[1].SequenceNo);
    }

    /// <summary>
    /// zh-CN: 验证按 Frame 回放消息时，会保留跨 Frame 的父消息链路，并映射用量与耗时指标。
    /// en: Verifies frame-level message replay preserves cross-frame parent lineage and maps usage and timing metrics.
    /// </summary>
    [Fact]
    public async Task GetFrameMessagesAsync_ReturnsCrossFrameParentLineageAndMetrics()
    {
        using var context = new TestContext();
        var projectId = Guid.NewGuid();
        await context.AddProjectAsync(projectId, "Frame Replay");

        var session = new ChatSession
        {
            ProjectId = projectId,
            Scene = SessionSceneTypes.ProjectGroup,
            Status = SessionStatus.Active,
            InitialInput = "session"
        };
        context.Db.ChatSessions.Add(session);

        var rootFrame = new ChatFrame
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Depth = 0,
            Purpose = "安排任务",
            Status = FrameStatus.Completed
        };
        var childFrame = new ChatFrame
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ParentFrameId = rootFrame.Id,
            Depth = 1,
            Purpose = "请实现登录接口",
            Status = FrameStatus.Active
        };
        context.Db.ChatFrames.AddRange(rootFrame, childFrame);

        var rootUserMessageId = Guid.NewGuid();
        var rootAssistantMessageId = Guid.NewGuid();
        var childEntryMessageId = Guid.NewGuid();
        context.Db.ChatMessages.AddRange(
            new ChatMessage
            {
                Id = rootUserMessageId,
                SessionId = session.Id,
                FrameId = rootFrame.Id,
                Role = MessageRoles.User,
                Content = "我要登录功能",
                SequenceNo = 0
            },
            new ChatMessage
            {
                Id = rootAssistantMessageId,
                SessionId = session.Id,
                FrameId = rootFrame.Id,
                ParentMessageId = rootUserMessageId,
                Role = MessageRoles.Assistant,
                Content = "我来安排给 Producer",
                SequenceNo = 1
            },
            new ChatMessage
            {
                Id = childEntryMessageId,
                SessionId = session.Id,
                FrameId = childFrame.Id,
                ParentMessageId = rootAssistantMessageId,
                Role = MessageRoles.Assistant,
                Content = "请实现登录接口",
                ContentType = MessageContentTypes.Internal,
                SequenceNo = 0
            },
            new ChatMessage
            {
                SessionId = session.Id,
                FrameId = childFrame.Id,
                ParentMessageId = childEntryMessageId,
                Role = MessageRoles.Assistant,
                Content = "已实现登录接口。",
                SequenceNo = 1,
                TokenUsage = """{"inputTokens":8,"outputTokens":13,"totalTokens":21}""",
                DurationMs = 900
            });
        await context.Db.SaveChangesAsync();

        var result = await context.Service.GetFrameMessagesAsync(new GetFrameMessagesRequest
        {
            SessionId = session.Id,
            FrameId = childFrame.Id
        }, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(childEntryMessageId, result[0].Id);
        Assert.Equal(rootAssistantMessageId, result[0].ParentMessageId);
        Assert.Equal(MessageContentTypes.Internal, result[0].ContentType);
        Assert.Equal(childEntryMessageId, result[1].ParentMessageId);
        Assert.Equal(21, result[1].TokenUsage);
        Assert.NotNull(result[1].Usage);
        Assert.Equal(8, result[1].Usage!.InputTokens);
        Assert.Equal(13, result[1].Usage!.OutputTokens);
        Assert.NotNull(result[1].Timing);
        Assert.Equal(900, result[1].Timing!.TotalMs);
    }

    private sealed class TestContext : IDisposable
    {
        private readonly SqliteConnection _connection;

        /// <summary>
        /// zh-CN: 构建带迁移的内存数据库和流管理器，让会话应用服务测试覆盖真实持久化与实时回放路径。
        /// en: Builds an in-memory database with migrations and a stream manager so session service tests cover real persistence and replay paths.
        /// </summary>
        public TestContext()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            Services = new ServiceCollection()
                .AddLogging()
                .AddDbContext<AppDbContext>(options => options.UseSqlite(_connection))
                .BuildServiceProvider();

            Db = Services.GetRequiredService<AppDbContext>();
            Db.Database.EnsureCreated();

            StreamManager = new SessionStreamManager(
                Services.GetRequiredService<IServiceScopeFactory>(),
                Services.GetRequiredService<ILogger<SessionStreamManager>>());
            Service = new SessionApiService(
                null!,
                StreamManager,
                new ChatSessionRepository(Db),
                new ChatMessageRepository(Db),
                new SessionEventRepository(Db),
                new ProjectRepository(Db));
        }

        public ServiceProvider Services { get; }
        public AppDbContext Db { get; }
        public SessionStreamManager StreamManager { get; }
        public SessionApiService Service { get; }

        /// <summary>
        /// zh-CN: 以最小必需字段插入项目记录，便于测试依赖项目阶段和状态的会话守卫逻辑。
        /// en: Inserts a project with the minimal required fields so tests can exercise session guards that depend on project phase and status.
        /// </summary>
        public async Task<Guid> AddProjectAsync(Guid projectId, string name, string phase = ProjectPhases.Brainstorming)
        {
            Db.Projects.Add(new Project
            {
                Id = projectId,
                Name = name,
                Status = ProjectStatus.Active,
                Phase = phase,
                Language = "zh-CN"
            });
            await Db.SaveChangesAsync();
            return projectId;
        }

        /// <summary>
        /// zh-CN: 释放流管理器、数据库和服务容器，确保每个测试都使用隔离的会话状态。
        /// en: Disposes the stream manager, database, and service container so each test runs with isolated session state.
        /// </summary>
        public void Dispose()
        {
            StreamManager.Dispose();
            Db.Dispose();
            Services.Dispose();
            _connection.Dispose();
        }
    }
}

