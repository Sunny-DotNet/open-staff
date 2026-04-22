using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenStaff.Application.Conversations.Models;
using OpenStaff.Application.Conversations.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class ConversationTriggerServiceTests
{
    /// <summary>
    /// zh-CN: 验证系统触发型入口会自动创建场景会话并落一条角色开场消息，
    /// 这样项目初始化、阶段切换等业务动作就不需要再自己手搓 ChatSession / ChatFrame / ChatMessage。
    /// en: Verifies the system trigger entry creates a scene session and a seeded role-authored message.
    /// </summary>
    [Fact]
    public async Task TriggerProjectSceneMessageAsync_CreatesSessionFrameAndAssistantMessage()
    {
        await using var db = await CreateDbContextAsync();
        var project = new Project
        {
            Name = "Trigger Project",
            Language = "zh-CN"
        };
        var secretary = new AgentRole
        {
            Name = "Monica",
            JobTitle = BuiltinRoleTypes.Secretary,
            IsBuiltin = true,
            IsActive = true
        };
        db.Projects.Add(project);
        db.AgentRoles.Add(secretary);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.TriggerProjectSceneMessageAsync(
            new ProjectConversationTriggerEntry(
                project.Id,
                SessionSceneTypes.ProjectBrainstorm,
                "系统已触发头脑风暴",
                "系统已触发头脑风暴",
                "你好，我是秘书。",
                AuthorRole: BuiltinRoleTypes.Secretary),
            CancellationToken.None);

        var sessions = await db.ChatSessions.AsNoTracking().ToListAsync();
        var frames = await db.ChatFrames.AsNoTracking().ToListAsync();
        var messages = await db.ChatMessages.AsNoTracking().ToListAsync();
        var session = Assert.Single(sessions);
        var frame = Assert.Single(frames);
        var message = Assert.Single(messages);

        Assert.True(result.CreatedSession);
        Assert.True(result.CreatedMessage);
        Assert.False(result.Skipped);
        Assert.Equal(session.Id, result.SessionId);
        Assert.Equal(frame.Id, result.FrameId);
        Assert.Equal(message.Id, result.MessageId);
        Assert.Equal(SessionSceneTypes.ProjectBrainstorm, session.Scene);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal("系统已触发头脑风暴", session.InitialInput);
        Assert.Equal(FrameStatus.Completed, frame.Status);
        Assert.Equal("系统已触发头脑风暴", frame.Purpose);
        Assert.Equal(MessageRoles.Assistant, message.Role);
        Assert.Equal(secretary.Id, message.AgentRoleId);
        Assert.Equal("你好，我是秘书。", message.Content);
    }

    /// <summary>
    /// zh-CN: 验证当同场景已经存在消息时，系统触发型入口会按幂等策略跳过，避免重复开场白。
    /// en: Verifies the system trigger entry skips scene seeding when messages already exist.
    /// </summary>
    [Fact]
    public async Task TriggerProjectSceneMessageAsync_SkipsWhenSceneAlreadyHasMessages()
    {
        await using var db = await CreateDbContextAsync();
        var project = new Project
        {
            Name = "Existing Trigger Project",
            Language = "zh-CN"
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var session = new ChatSession
        {
            ProjectId = project.Id,
            Scene = SessionSceneTypes.ProjectBrainstorm,
            Status = SessionStatus.Active,
            InitialInput = "已有摘要"
        };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync();

        var frame = new ChatFrame
        {
            SessionId = session.Id,
            Depth = 0,
            Status = FrameStatus.Completed,
            Purpose = "已有目的"
        };
        db.ChatFrames.Add(frame);
        await db.SaveChangesAsync();

        var message = new ChatMessage
        {
            SessionId = session.Id,
            FrameId = frame.Id,
            Role = MessageRoles.Assistant,
            Content = "已有消息",
            SequenceNo = 0
        };
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.TriggerProjectSceneMessageAsync(
            new ProjectConversationTriggerEntry(
                project.Id,
                SessionSceneTypes.ProjectBrainstorm,
                "新的摘要",
                "新的目的",
                "新的消息",
                AuthorRole: BuiltinRoleTypes.Secretary),
            CancellationToken.None);

        var sessions = await db.ChatSessions.AsNoTracking().ToListAsync();
        var frames = await db.ChatFrames.AsNoTracking().ToListAsync();
        var messages = await db.ChatMessages.AsNoTracking().ToListAsync();

        Assert.False(result.CreatedSession);
        Assert.False(result.CreatedMessage);
        Assert.True(result.Skipped);
        Assert.Equal(session.Id, result.SessionId);
        Assert.Equal(frame.Id, result.FrameId);
        Assert.Equal(message.Id, result.MessageId);
        Assert.Single(sessions);
        Assert.Single(frames);
        Assert.Single(messages);
    }

    private static ConversationTriggerService CreateService(AppDbContext db)
    {
        return new ConversationTriggerService(
            new ChatSessionRepository(db),
            new ChatFrameRepository(db),
            new ChatMessageRepository(db),
            new ProjectAgentRoleRepository(db),
            new AgentRoleRepository(db),
            db,
            NullLogger<ConversationTriggerService>.Instance);
    }

    private static async Task<AppDbContext> CreateDbContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
