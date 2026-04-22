using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenStaff.Agent.Services;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Entities;
using OpenStaff.Core.Notifications;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Repositories;

namespace OpenStaff.Tests.Unit;

public class AgentMessageObserversTests
{
    /// <summary>
    /// zh-CN: 验证流式消息观察器会把文本分片和工具调用分别投影成会话事件，供前端实时展示。
    /// en: Verifies the streaming observer projects text chunks and tool calls into session events for real-time UI updates.
    /// </summary>
    [Fact]
    public async Task SessionStreamingAgentMessageObserver_PublishesStreamingAndToolEvents()
    {
        var notificationMock = new Mock<INotificationService>();
        var observer = new SessionStreamingAgentMessageObserver(notificationMock.Object);
        var sessionId = Guid.NewGuid();
        var frameId = Guid.NewGuid();
        var context = new MessageContext(
            ProjectId: Guid.NewGuid(),
            SessionId: sessionId,
            ParentMessageId: null,
            FrameId: frameId,
            ParentFrameId: null,
            TaskId: null,
            ProjectAgentRoleId: null,
            TargetRole: "producer",
            InitiatorRole: "user",
            Extra: null);

        await observer.PublishAsync(
            new AgentMessageEvent
            {
                MessageId = Guid.NewGuid(),
                EventType = AgentMessageEventType.ContentChunk,
                Scene = MessageScene.ProjectGroup,
                Context = context,
                Text = "hello"
            },
            CancellationToken.None);

        await observer.PublishAsync(
            new AgentMessageEvent
            {
                MessageId = Guid.NewGuid(),
                EventType = AgentMessageEventType.ToolCall,
                Scene = MessageScene.ProjectGroup,
                Context = context,
                ToolName = "search",
                ToolCallId = "call-1",
                ToolArguments = "{\"q\":\"abc\"}"
            },
            CancellationToken.None);

        notificationMock.Verify(
            item => item.PublishSessionEventAsync(
                sessionId,
                It.Is<SessionEvent>(evt =>
                    evt.FrameId == frameId
                    && evt.EventType == SessionEventTypes.StreamingToken
                    && evt.Payload != null
                    && JsonDocument.Parse(evt.Payload).RootElement.GetProperty("token").GetString() == "hello"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        notificationMock.Verify(
            item => item.PublishSessionEventAsync(
                sessionId,
                It.Is<SessionEvent>(evt =>
                    evt.FrameId == frameId
                    && evt.EventType == SessionEventTypes.ToolCall
                    && evt.Payload != null
                    && JsonDocument.Parse(evt.Payload).RootElement.GetProperty("name").GetString() == "search"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// zh-CN: 验证重试和取消会被映射为可操作的会话事件，确保客户端能区分暂时性失败与终止状态。
    /// en: Verifies retry and cancellation are projected as actionable session events so clients can distinguish transient failures from termination.
    /// </summary>
    [Fact]
    public async Task SessionStreamingAgentMessageObserver_PublishesRetryAndCancelledEvents()
    {
        var notificationMock = new Mock<INotificationService>();
        var observer = new SessionStreamingAgentMessageObserver(notificationMock.Object);
        var sessionId = Guid.NewGuid();
        var frameId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var context = new MessageContext(
            ProjectId: Guid.NewGuid(),
            SessionId: sessionId,
            ParentMessageId: null,
            FrameId: frameId,
            ParentFrameId: null,
            TaskId: null,
            ProjectAgentRoleId: null,
            TargetRole: "producer",
            InitiatorRole: "secretary",
            Extra: null);

        await observer.PublishAsync(
            new AgentMessageEvent
            {
                MessageId = messageId,
                EventType = AgentMessageEventType.RetryScheduled,
                Scene = MessageScene.ProjectGroup,
                Context = context,
                Attempt = 2,
                Error = "network timeout"
            },
            CancellationToken.None);

        await observer.PublishAsync(
            new AgentMessageEvent
            {
                MessageId = messageId,
                EventType = AgentMessageEventType.Cancelled,
                Scene = MessageScene.ProjectGroup,
                Context = context,
                Attempt = 2,
                AgentRole = "producer",
                Model = "gpt-4.1",
                Error = "Message execution cancelled."
            },
            CancellationToken.None);

        notificationMock.Verify(
            item => item.PublishSessionEventAsync(
                sessionId,
                It.Is<SessionEvent>(evt =>
                    evt.MessageId == messageId
                    && evt.FrameId == frameId
                    && evt.EventType == SessionEventTypes.Action
                    && evt.Payload != null
                    && JsonDocument.Parse(evt.Payload).RootElement.GetProperty("type").GetString() == "retry_scheduled"
                    && JsonDocument.Parse(evt.Payload).RootElement.GetProperty("attempt").GetInt32() == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);

        notificationMock.Verify(
            item => item.PublishSessionEventAsync(
                sessionId,
                It.Is<SessionEvent>(evt =>
                    evt.MessageId == messageId
                    && evt.FrameId == frameId
                    && evt.EventType == SessionEventTypes.Error
                    && evt.Payload != null
                    && JsonDocument.Parse(evt.Payload).RootElement.GetProperty("cancelled").GetBoolean()
                    && JsonDocument.Parse(evt.Payload).RootElement.GetProperty("agent").GetString() == "producer"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// zh-CN: 验证完成事件会发出 streaming_done 通知，帮助消费方正确结束流式渲染。
    /// en: Verifies a completed event emits the streaming_done notification so consumers can close streaming rendering cleanly.
    /// </summary>
    [Fact]
    public async Task SessionStreamingAgentMessageObserver_Completed_PublishesStreamingDone()
    {
        var notificationMock = new Mock<INotificationService>();
        var observer = new SessionStreamingAgentMessageObserver(notificationMock.Object);
        var sessionId = Guid.NewGuid();
        var frameId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var context = new MessageContext(
            ProjectId: Guid.NewGuid(),
            SessionId: sessionId,
            ParentMessageId: null,
            FrameId: frameId,
            ParentFrameId: null,
            TaskId: null,
            ProjectAgentRoleId: null,
            TargetRole: "producer",
            InitiatorRole: "secretary",
            Extra: null);

        await observer.PublishAsync(
            new AgentMessageEvent
            {
                MessageId = messageId,
                EventType = AgentMessageEventType.Completed,
                Scene = MessageScene.ProjectGroup,
                Context = context,
                AgentRole = "producer",
                Model = "gpt-4.1",
                Usage = new MessageUsageSnapshot(10, 20, 30),
                Timing = new MessageTimingSnapshot(500, 100),
                IsTerminal = true
            },
            CancellationToken.None);

        notificationMock.Verify(
            item => item.PublishSessionEventAsync(
                sessionId,
                It.Is<SessionEvent>(evt =>
                    evt.MessageId == messageId
                    && evt.FrameId == frameId
                    && evt.EventType == SessionEventTypes.StreamingDone),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// zh-CN: 验证消息投影观察器会在完成时持久化最终助理消息，并发布带有用量和耗时的终态事件。
    /// en: Verifies the projection observer persists the final assistant message on completion and publishes a terminal event with usage and timing data.
    /// </summary>
    [Fact]
    public async Task ChatMessageProjectionObserver_Completed_PersistsMessageAndPublishesFinalEvent()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var notificationMock = new Mock<INotificationService>();
        var services = new ServiceCollection()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>())
            .AddScoped<IChatMessageRepository>(sp => new ChatMessageRepository(sp.GetRequiredService<AppDbContext>()))
            .BuildServiceProvider();

        var sessionId = Guid.NewGuid();
        var frameId = Guid.NewGuid();
        var entryMessageId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Observer Test"
            });
            db.ChatSessions.Add(new ChatSession
            {
                Id = sessionId,
                ProjectId = projectId,
                InitialInput = "开始",
                Scene = SessionSceneTypes.ProjectBrainstorm
            });
            db.ChatFrames.Add(new ChatFrame
            {
                Id = frameId,
                SessionId = sessionId,
                Depth = 0,
                Purpose = "开始"
            });
            db.ChatMessages.Add(new ChatMessage
            {
                Id = entryMessageId,
                SessionId = sessionId,
                FrameId = frameId,
                Role = MessageRoles.User,
                Content = "开始",
                SequenceNo = 1
            });
            await db.SaveChangesAsync();
        }

        var observer = new ChatMessageProjectionObserver(
            services.GetRequiredService<IServiceScopeFactory>(),
            notificationMock.Object);

        var context = new MessageContext(
            ProjectId: projectId,
            SessionId: sessionId,
            ParentMessageId: entryMessageId,
            FrameId: frameId,
            ParentFrameId: null,
            TaskId: null,
            ProjectAgentRoleId: null,
            TargetRole: "secretary",
            InitiatorRole: "user",
            Extra: null);
        var assistantMessageId = Guid.NewGuid();

        await observer.PublishAsync(
            new AgentMessageEvent
            {
                MessageId = assistantMessageId,
                EventType = AgentMessageEventType.ContentChunk,
                Scene = MessageScene.ProjectBrainstorm,
                Context = context,
                AgentRole = "secretary",
                Model = "gpt-4.1",
                Text = "整理好了"
            },
            CancellationToken.None);

        await observer.PublishAsync(
            new AgentMessageEvent
            {
                MessageId = assistantMessageId,
                EventType = AgentMessageEventType.Completed,
                Scene = MessageScene.ProjectBrainstorm,
                Context = context,
                AgentRole = "secretary",
                Model = "gpt-4.1",
                Usage = new MessageUsageSnapshot(10, 20, 30),
                Timing = new MessageTimingSnapshot(500, 100),
                IsTerminal = true
            },
            CancellationToken.None);

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.ChatMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == assistantMessageId);

            Assert.NotNull(saved);
            Assert.Equal(entryMessageId, saved.ParentMessageId);
            Assert.Equal("整理好了", saved.Content);
            Assert.Equal(2, saved.SequenceNo);
            Assert.NotNull(saved.TokenUsage);
            Assert.Equal(500, saved.DurationMs);
            using var usageDocument = JsonDocument.Parse(saved.TokenUsage!);
            Assert.Equal(30, usageDocument.RootElement.GetProperty("TotalTokens").GetInt32());
        }

        notificationMock.Verify(
            item => item.PublishSessionEventAsync(
                sessionId,
                It.Is<SessionEvent>(evt =>
                    evt.MessageId == assistantMessageId
                    && evt.EventType == SessionEventTypes.Message
                    && evt.Payload != null
                    && JsonDocument.Parse(evt.Payload).RootElement.GetProperty("content").GetString() == "整理好了"
                    && JsonDocument.Parse(evt.Payload).RootElement.GetProperty("usage").GetProperty("TotalTokens").GetInt32() == 30
                    && JsonDocument.Parse(evt.Payload).RootElement.GetProperty("timing").GetProperty("TotalMs").GetInt32() == 500
                    && JsonDocument.Parse(evt.Payload).RootElement.GetProperty("model").GetString() == "gpt-4.1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// zh-CN: 验证当终态事件重复携带同样的文本时，投影层不会把最终内容追加两次。
    /// en: Verifies the projection layer does not append terminal content twice when the final event repeats the same text.
    /// </summary>
    [Fact]
    public async Task ChatMessageProjectionObserver_Completed_DoesNotDuplicateTerminalContent()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var notificationMock = new Mock<INotificationService>();
        var services = new ServiceCollection()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>())
            .AddScoped<IChatMessageRepository>(sp => new ChatMessageRepository(sp.GetRequiredService<AppDbContext>()))
            .BuildServiceProvider();

        var sessionId = Guid.NewGuid();
        var frameId = Guid.NewGuid();
        var entryMessageId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Observer Duplication Test"
            });
            db.ChatSessions.Add(new ChatSession
            {
                Id = sessionId,
                ProjectId = projectId,
                InitialInput = "开始",
                Scene = SessionSceneTypes.ProjectBrainstorm
            });
            db.ChatFrames.Add(new ChatFrame
            {
                Id = frameId,
                SessionId = sessionId,
                Depth = 0,
                Purpose = "开始"
            });
            db.ChatMessages.Add(new ChatMessage
            {
                Id = entryMessageId,
                SessionId = sessionId,
                FrameId = frameId,
                Role = MessageRoles.User,
                Content = "开始",
                SequenceNo = 1
            });
            await db.SaveChangesAsync();
        }

        var observer = new ChatMessageProjectionObserver(
            services.GetRequiredService<IServiceScopeFactory>(),
            notificationMock.Object);

        var context = new MessageContext(
            ProjectId: projectId,
            SessionId: sessionId,
            ParentMessageId: entryMessageId,
            FrameId: frameId,
            ParentFrameId: null,
            TaskId: null,
            ProjectAgentRoleId: null,
            TargetRole: "secretary",
            InitiatorRole: "user",
            Extra: null);
        var assistantMessageId = Guid.NewGuid();

        await observer.PublishAsync(
            new AgentMessageEvent
            {
                MessageId = assistantMessageId,
                EventType = AgentMessageEventType.ContentChunk,
                Scene = MessageScene.ProjectBrainstorm,
                Context = context,
                AgentRole = "secretary",
                Text = "整理好了"
            },
            CancellationToken.None);

        await observer.PublishAsync(
            new AgentMessageEvent
            {
                MessageId = assistantMessageId,
                EventType = AgentMessageEventType.Completed,
                Scene = MessageScene.ProjectBrainstorm,
                Context = context,
                AgentRole = "secretary",
                Text = "整理好了",
                IsTerminal = true
            },
            CancellationToken.None);

        await using var verifyScope = services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await verifyDb.ChatMessages.AsNoTracking().SingleAsync(item => item.Id == assistantMessageId);

        Assert.Equal("整理好了", saved.Content);
    }

    /// <summary>
    /// zh-CN: 验证运行时监控观察器会记录代理开始/完成事件，并同步刷新任务的运行时元数据。
    /// en: Verifies the runtime monitoring observer records agent start/completion events and updates task runtime metadata in step.
    /// </summary>
    [Fact]
    public async Task RuntimeMonitoringProjectionObserver_ProjectsAgentEventsAndTaskMetadata()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>())
            .AddScoped<IAgentEventRepository>(sp => new AgentEventRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<ITaskItemRepository>(sp => new TaskItemRepository(sp.GetRequiredService<AppDbContext>()))
            .BuildServiceProvider();

        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var frameId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var projectAgentId = Guid.NewGuid();
        var agentRoleId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Runtime Monitoring"
            });
            db.AgentRoles.Add(new AgentRole
            {
                Id = agentRoleId,
                Name = "Producer",
                JobTitle = "producer",
                IsActive = true
            });
            db.ProjectAgentRoles.Add(new ProjectAgentRole
            {
                Id = projectAgentId,
                ProjectId = projectId,
                AgentRoleId = agentRoleId
            });
            db.ChatSessions.Add(new ChatSession
            {
                Id = sessionId,
                ProjectId = projectId,
                InitialInput = "开始",
                Scene = SessionSceneTypes.ProjectGroup
            });
            db.ChatFrames.Add(new ChatFrame
            {
                Id = frameId,
                SessionId = sessionId,
                TaskId = taskId,
                Depth = 0,
                Purpose = "编写 API"
            });
            db.Tasks.Add(new TaskItem
            {
                Id = taskId,
                ProjectId = projectId,
                Title = "编写 API",
                AssignedProjectAgentRoleId = projectAgentId,
                Metadata = JsonSerializer.Serialize(new TaskItemRuntimeMetadata
                {
                    SessionId = sessionId,
                    FrameId = frameId,
                    Scene = SessionSceneTypes.ProjectGroup
                })
            });
            await db.SaveChangesAsync();
        }

        var observer = new RuntimeMonitoringProjectionObserver(
            services.GetRequiredService<IServiceScopeFactory>());
        var context = new MessageContext(
            ProjectId: projectId,
            SessionId: sessionId,
            ParentMessageId: null,
            FrameId: frameId,
            ParentFrameId: null,
            TaskId: taskId,
            ProjectAgentRoleId: projectAgentId,
            TargetRole: "producer",
            InitiatorRole: "secretary",
            Extra: null);

        await observer.PublishAsync(
            new AgentMessageEvent
            {
                MessageId = messageId,
                EventType = AgentMessageEventType.Started,
                Scene = MessageScene.ProjectGroup,
                Context = context,
                AgentRole = "producer",
                Attempt = 1
            },
            CancellationToken.None);

        await observer.PublishAsync(
            new AgentMessageEvent
            {
                MessageId = messageId,
                EventType = AgentMessageEventType.Completed,
                Scene = MessageScene.ProjectGroup,
                Context = context,
                AgentRole = "producer",
                Model = "gpt-4.1",
                Text = "已完成后端 API 初稿。",
                Usage = new MessageUsageSnapshot(10, 20, 30),
                Timing = new MessageTimingSnapshot(500, 120),
                Attempt = 1,
                IsTerminal = true
            },
            CancellationToken.None);

        await using var verifyScope = services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var events = await verifyDb.AgentEvents
            .AsNoTracking()
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync();
        var task = await verifyDb.Tasks.AsNoTracking().SingleAsync(item => item.Id == taskId);

        Assert.Collection(events,
            started =>
            {
                Assert.Equal(EventTypes.RunStarted, started.EventType);
                Assert.Equal(projectAgentId, started.ProjectAgentRoleId);
                Assert.Null(started.ParentEventId);
            },
            completed =>
            {
                Assert.Equal(EventTypes.Message, completed.EventType);
                Assert.Equal(events[0].Id, completed.ParentEventId);
                var metadata = AgentEventMetadataPayload.TryParse(completed.Metadata);
                Assert.NotNull(metadata);
                Assert.Equal(taskId, metadata!.TaskId);
                Assert.Equal(messageId, metadata.MessageId);
                Assert.Equal(SessionSceneTypes.ProjectGroup, metadata.Scene);
                Assert.Equal("gpt-4.1", metadata.Model);
                Assert.Equal(TaskItemRuntimeMetadata.MaxAttempts, metadata.MaxAttempts);
                Assert.Equal(30, metadata.TotalTokens);
            });

        var taskMetadata = TaskItemRuntimeMetadata.TryParse(task.Metadata);
        Assert.NotNull(taskMetadata);
        Assert.Equal(messageId, taskMetadata!.MessageId);
        Assert.Equal(SessionSceneTypes.ProjectGroup, taskMetadata.Scene);
        Assert.Equal(TaskItemStatus.Done, taskMetadata.LastStatus);
        Assert.Equal("已完成后端 API 初稿。", taskMetadata.LastResult);
        Assert.Equal("gpt-4.1", taskMetadata.Model);
        Assert.Equal(0, taskMetadata.AttemptCount);
        Assert.Equal(30, taskMetadata.TotalTokens);
    }

    /// <summary>
    /// zh-CN: 验证显式跳过最终投影时，不会持久化助理消息，也不会向会话流发送终态通知。
    /// en: Verifies that opting out of final projection suppresses assistant-message persistence and skips session notifications.
    /// </summary>
    [Fact]
    public async Task ChatMessageProjectionObserver_WithSkipFinalProjection_DoesNotPersistAssistantMessage()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var notificationMock = new Mock<INotificationService>();
        var services = new ServiceCollection()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>())
            .AddScoped<IChatMessageRepository>(sp => new ChatMessageRepository(sp.GetRequiredService<AppDbContext>()))
            .BuildServiceProvider();

        var sessionId = Guid.NewGuid();
        var frameId = Guid.NewGuid();
        var entryMessageId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Skip Projection Test"
            });
            db.ChatSessions.Add(new ChatSession
            {
                Id = sessionId,
                ProjectId = projectId,
                InitialInput = "开始",
                Scene = SessionSceneTypes.ProjectGroup
            });
            db.ChatFrames.Add(new ChatFrame
            {
                Id = frameId,
                SessionId = sessionId,
                Depth = 0,
                Purpose = "开始"
            });
            db.ChatMessages.Add(new ChatMessage
            {
                Id = entryMessageId,
                SessionId = sessionId,
                FrameId = frameId,
                Role = MessageRoles.User,
                Content = "开始",
                SequenceNo = 1
            });
            await db.SaveChangesAsync();
        }

        var observer = new ChatMessageProjectionObserver(
            services.GetRequiredService<IServiceScopeFactory>(),
            notificationMock.Object);

        var context = new MessageContext(
            ProjectId: projectId,
            SessionId: sessionId,
            ParentMessageId: entryMessageId,
            FrameId: frameId,
            ParentFrameId: null,
            TaskId: null,
            ProjectAgentRoleId: null,
            TargetRole: "producer",
            InitiatorRole: "user",
            Extra: new Dictionary<string, string>
            {
                ["skip_final_projection"] = "true"
            });
        var assistantMessageId = Guid.NewGuid();

        await observer.PublishAsync(
            new AgentMessageEvent
            {
                MessageId = assistantMessageId,
                EventType = AgentMessageEventType.ContentChunk,
                Scene = MessageScene.ProjectGroup,
                Context = context,
                AgentRole = "producer",
                Text = "这是运行时返回"
            },
            CancellationToken.None);

        await observer.PublishAsync(
            new AgentMessageEvent
            {
                MessageId = assistantMessageId,
                EventType = AgentMessageEventType.Completed,
                Scene = MessageScene.ProjectGroup,
                Context = context,
                AgentRole = "producer",
                IsTerminal = true
            },
            CancellationToken.None);

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Single(await db.ChatMessages.AsNoTracking().ToListAsync());
            Assert.Null(await db.ChatMessages.AsNoTracking().FirstOrDefaultAsync(item => item.Id == assistantMessageId));
        }

        notificationMock.Verify(
            item => item.PublishSessionEventAsync(
                It.IsAny<Guid>(),
                It.IsAny<SessionEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
