using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Application.Sessions.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Repositories;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class SessionStreamManagerTests
{
    [Fact]
    public async Task SubscribeAsync_AfterSequenceNo_SkipsBufferedReplayEvents()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>())
            .AddScoped<ISessionEventRepository, SessionEventRepository>()
            .BuildServiceProvider();

        using (var scope = services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        }

        var sessionId = Guid.NewGuid();
        using var manager = new SessionStreamManager(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ILogger<SessionStreamManager>>());
        var stream = manager.Create(sessionId);
        stream.Push(new SessionEvent
        {
            EventType = SessionEventTypes.StreamingToken,
            Payload = """{"token":"alpha"}""",
            CreatedAt = DateTime.UtcNow
        });
        stream.Push(new SessionEvent
        {
            EventType = SessionEventTypes.StreamingToken,
            Payload = """{"token":"beta"}""",
            CreatedAt = DateTime.UtcNow
        });
        stream.Push(new SessionEvent
        {
            EventType = SessionEventTypes.Message,
            Payload = """{"content":"done"}""",
            CreatedAt = DateTime.UtcNow
        });
        manager.CompleteTransient(sessionId, TimeSpan.FromMinutes(1));

        var replayed = new List<SessionEvent>();
        await foreach (var evt in manager.SubscribeAsync(sessionId, afterSequenceNo: 2))
        {
            replayed.Add(evt);
        }

        var replay = Assert.Single(replayed);
        Assert.Equal(SessionEventTypes.Message, replay.EventType);
        Assert.Equal(3, replay.SequenceNo);
        Assert.Equal("""{"content":"done"}""", replay.Payload);
    }

    [Fact]
    public async Task CompleteAsync_PersistsBufferedEventsAndSubscribeAsync_ReplaysPersistedHistory()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>())
            .AddScoped<ISessionEventRepository, SessionEventRepository>()
            .BuildServiceProvider();

        var sessionId = Guid.NewGuid();
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            var projectId = Guid.NewGuid();
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Replay Project",
                Status = ProjectStatus.Active,
                Phase = ProjectPhases.Brainstorming,
                Language = "zh-CN"
            });
            db.ChatSessions.Add(new ChatSession
            {
                Id = sessionId,
                ProjectId = projectId,
                Scene = SessionSceneTypes.ProjectBrainstorm,
                Status = SessionStatus.Completed,
                InitialInput = "hello"
            });
            await db.SaveChangesAsync();
        }

        using var manager = new SessionStreamManager(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ILogger<SessionStreamManager>>());
        var stream = manager.Create(sessionId);
        stream.Push(new SessionEvent
        {
            EventType = SessionEventTypes.Message,
            Payload = """{"content":"persisted"}""",
            CreatedAt = DateTime.UtcNow
        });

        await manager.CompleteAsync(sessionId);

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persisted = await db.SessionEvents
                .Where(evt => evt.SessionId == sessionId)
                .OrderBy(evt => evt.SequenceNo)
                .ToListAsync();

            var evt = Assert.Single(persisted);
            Assert.Equal(SessionEventTypes.Message, evt.EventType);
            Assert.Equal(1, evt.SequenceNo);
            Assert.Equal("""{"content":"persisted"}""", evt.Payload);
        }

        var replayed = new List<SessionEvent>();
        await foreach (var evt in manager.SubscribeAsync(sessionId))
        {
            replayed.Add(evt);
        }

        var replay = Assert.Single(replayed);
        Assert.Equal(SessionEventTypes.Message, replay.EventType);
        Assert.Equal(1, replay.SequenceNo);
        Assert.Equal("""{"content":"persisted"}""", replay.Payload);
    }
}

