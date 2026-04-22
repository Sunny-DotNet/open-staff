using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;
using OpenStaff.Application.Providers.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Infrastructure.Security;
using OpenStaff.Options;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Tests.Unit;

public class MonitorApiServiceTests
{
    /// <summary>
    /// zh-CN: 验证总览统计中的最近会话会携带项目标识与项目名称，便于监控界面直接回显上下文。
    /// en: Verifies that recent sessions in the overview stats include project identifiers and names so the monitoring UI can display context directly.
    /// </summary>
    [Fact]
    public async Task GetStatsAsync_ReturnsRecentSessionsWithProjectContext()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Monitor Stats");

        context.Db.ChatSessions.Add(new ChatSession
        {
            ProjectId = project.Id,
            Scene = SessionSceneTypes.ProjectGroup,
            Status = SessionStatus.Active,
            InitialInput = "继续推进项目",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await context.Db.SaveChangesAsync();

        var result = await context.Service.GetStatsAsync(CancellationToken.None);

        var recent = Assert.Single(result.RecentSessions);
        Assert.Equal(project.Id, recent.ProjectId);
        Assert.Equal(project.Name, recent.ProjectName);
        Assert.Equal(SessionSceneTypes.ProjectGroup, recent.Scene);
        Assert.Equal("继续推进项目", recent.Input);
    }

    /// <summary>
    /// zh-CN: 验证项目统计会按场景聚合会话、任务与事件数据，确保监控报表反映真实执行分布。
    /// en: Verifies that project stats aggregate sessions, tasks, and events by scene so monitoring reports reflect the actual execution mix.
    /// </summary>
    [Fact]
    public async Task GetProjectStatsAsync_ReturnsSceneBreakdown()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Scene Breakdown");
        var agent = await context.AddProjectAgentAsync(project.Id, "Producer", "producer");

        context.Db.ChatSessions.AddRange(
            new ChatSession
            {
                ProjectId = project.Id,
                Scene = SessionSceneTypes.ProjectBrainstorm,
                Status = SessionStatus.Completed,
                InitialInput = "讨论需求"
            },
            new ChatSession
            {
                ProjectId = project.Id,
                Scene = SessionSceneTypes.ProjectGroup,
                Status = SessionStatus.Active,
                InitialInput = "安排实现"
            });

        context.Db.Tasks.AddRange(
            new TaskItem
            {
                ProjectId = project.Id,
                Title = "整理需求",
                Status = TaskItemStatus.Done,
                Metadata = JsonSerializer.Serialize(new TaskItemRuntimeMetadata
                {
                    Scene = SessionSceneTypes.ProjectBrainstorm,
                    LastStatus = TaskItemStatus.Done
                })
            },
            new TaskItem
            {
                ProjectId = project.Id,
                Title = "实现接口",
                Status = TaskItemStatus.InProgress,
                AssignedProjectAgentRoleId = agent.Id,
                Metadata = JsonSerializer.Serialize(new TaskItemRuntimeMetadata
                {
                    Scene = SessionSceneTypes.ProjectGroup,
                    LastStatus = TaskItemStatus.InProgress
                })
            });

        context.Db.AgentEvents.AddRange(
            new AgentEvent
            {
                ProjectId = project.Id,
                ProjectAgentRoleId = agent.Id,
                EventType = EventTypes.Message,
                Content = "已整理需求",
                Metadata = JsonSerializer.Serialize(new AgentEventMetadataPayload
                {
                    Scene = SessionSceneTypes.ProjectBrainstorm,
                    TotalTokens = 10,
                    DurationMs = 200
                })
            },
            new AgentEvent
            {
                ProjectId = project.Id,
                ProjectAgentRoleId = agent.Id,
                EventType = EventTypes.ToolCall,
                Content = "调用文件工具",
                Metadata = JsonSerializer.Serialize(new AgentEventMetadataPayload
                {
                    Scene = SessionSceneTypes.ProjectGroup,
                    ToolName = "file_system"
                })
            },
            new AgentEvent
            {
                ProjectId = project.Id,
                ProjectAgentRoleId = agent.Id,
                EventType = EventTypes.Message,
                Content = "已实现接口",
                Metadata = JsonSerializer.Serialize(new AgentEventMetadataPayload
                {
                    Scene = SessionSceneTypes.ProjectGroup,
                    TotalTokens = 30,
                    DurationMs = 500
                })
            });

        await context.Db.SaveChangesAsync();

        var result = await context.Service.GetProjectStatsAsync(project.Id, CancellationToken.None);

        Assert.Equal(2, result.EventsByType[EventTypes.Message]);
        Assert.Equal(1, result.EventsByType[EventTypes.ToolCall]);

        var brainstorm = Assert.Single(result.SceneBreakdown.Where(item => item.Scene == SessionSceneTypes.ProjectBrainstorm));
        Assert.Equal(1, brainstorm.SessionCount);
        Assert.Equal(1, brainstorm.TaskCount);
        Assert.Equal(1, brainstorm.EventCount);
        Assert.Equal(1, brainstorm.RunCount);
        Assert.Equal(10, brainstorm.TotalTokens);
        Assert.Equal(200, brainstorm.AverageDurationMs);

        var projectGroup = Assert.Single(result.SceneBreakdown.Where(item => item.Scene == SessionSceneTypes.ProjectGroup));
        Assert.Equal(1, projectGroup.SessionCount);
        Assert.Equal(1, projectGroup.TaskCount);
        Assert.Equal(2, projectGroup.EventCount);
        Assert.Equal(1, projectGroup.RunCount);
        Assert.Equal(30, projectGroup.TotalTokens);
        Assert.Equal(500, projectGroup.AverageDurationMs);
    }

    /// <summary>
    /// zh-CN: 验证事件查询的场景过滤只返回匹配场景的数据，避免监控列表混入其他执行阶段的记录。
    /// en: Verifies that event queries with a scene filter return only matching records so monitoring lists do not mix in other execution phases.
    /// </summary>
    [Fact]
    public async Task GetEventsAsync_WithSceneFilter_ReturnsOnlyMatchingScene()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Monitor Events");

        context.Db.AgentEvents.AddRange(
            new AgentEvent
            {
                ProjectId = project.Id,
                EventType = EventTypes.Message,
                Content = "brainstorm",
                Metadata = JsonSerializer.Serialize(new AgentEventMetadataPayload
                {
                    Scene = SessionSceneTypes.ProjectBrainstorm,
                })
            },
            new AgentEvent
            {
                ProjectId = project.Id,
                EventType = EventTypes.ToolCall,
                Content = "group tool",
                Metadata = JsonSerializer.Serialize(new AgentEventMetadataPayload
                {
                    Scene = SessionSceneTypes.ProjectGroup,
                    ToolName = "search"
                })
            },
            new AgentEvent
            {
                ProjectId = project.Id,
                EventType = EventTypes.Message,
                Content = "group message",
                Metadata = JsonSerializer.Serialize(new AgentEventMetadataPayload
                {
                    Scene = SessionSceneTypes.ProjectGroup,
                })
            });
        await context.Db.SaveChangesAsync();

        var result = await context.Service.GetEventsAsync(new GetEventsRequest
        {
            ProjectId = project.Id,
            Scene = SessionSceneTypes.ProjectGroup,
            Page = 1,
            PageSize = 10
        }, CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.All(result.Items, item => Assert.Equal(SessionSceneTypes.ProjectGroup, item.Scene));
    }

    /// <summary>
    /// zh-CN: 为监控测试封装内存数据库与最小依赖，减少每个测试重复搭建应用服务的样板代码。
    /// en: Wraps an in-memory database and minimal dependencies for monitor tests so each test avoids repeating service setup boilerplate.
    /// </summary>
    private sealed class TestContext : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly string _workingDirectory;

        /// <summary>
        /// zh-CN: 初始化可独立运行的监控测试环境，包括数据库、提供程序服务以及被测应用服务。
        /// en: Initializes a self-contained monitor test environment including the database, provider services, and the application service under test.
        /// </summary>
        public TestContext()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _workingDirectory = Path.Combine(Path.GetTempPath(), "openstaff-monitor-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workingDirectory);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            Db = new AppDbContext(options);
            Db.Database.EnsureCreated();

            var protocolFactory = new Mock<IProtocolFactory>();
            protocolFactory
                .Setup(factory => factory.GetProtocolEnvType(It.IsAny<string>()))
                .Returns((Type?)null);

            var hostEnvironment = new Mock<IHostEnvironment>();
            hostEnvironment
                .SetupGet(environment => environment.EnvironmentName)
                .Returns(Environments.Development);

            var providerAccountService = new ProviderAccountService(
                new ProviderAccountRepository(Db),
                Db,
                new EncryptionService("monitor-tests"),
                protocolFactory.Object,
                hostEnvironment.Object,
                Microsoft.Extensions.Options.Options.Create(new OpenStaffOptions
                {
                    WorkingDirectory = _workingDirectory
                }));

            Service = new MonitorApiService(
                new ProjectRepository(Db),
                new ProjectAgentRoleRepository(Db),
                new TaskItemRepository(Db),
                new AgentEventRepository(Db),
                new ChatSessionRepository(Db),
                new AgentRoleRepository(Db),
                new CheckpointRepository(Db),
                providerAccountService);
        }

        public AppDbContext Db { get; }
        public MonitorApiService Service { get; }

        /// <summary>
        /// zh-CN: 向测试数据库中插入项目实体，方便各统计测试快速获得有效项目上下文。
        /// en: Inserts a project entity into the test database so the stats tests can quickly obtain a valid project context.
        /// </summary>
        public async Task<Project> AddProjectAsync(string name)
        {
            var project = new Project
            {
                Name = name
            };
            Db.Projects.Add(project);
            await Db.SaveChangesAsync();
            return project;
        }

        /// <summary>
        /// zh-CN: 为指定项目创建带角色定义的项目代理，供场景统计和事件聚合测试复用。
        /// en: Creates a project agent with its role definition for a given project so scene-stat and event-aggregation tests can reuse it.
        /// </summary>
        public async Task<ProjectAgentRole> AddProjectAgentAsync(Guid projectId, string name, string roleType)
        {
            var role = new AgentRole
            {
                Id = Guid.NewGuid(),
                Name = name,
                JobTitle = roleType,
                IsActive = true
            };
            var agent = new ProjectAgentRole
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                AgentRoleId = role.Id,
                AgentRole = role,
                Status = AgentStatus.Idle
            };

            Db.AgentRoles.Add(role);
            Db.ProjectAgentRoles.Add(agent);
            await Db.SaveChangesAsync();
            return agent;
        }

        /// <summary>
        /// zh-CN: 释放数据库上下文与内存连接，确保每个测试隔离且不会泄漏本地资源。
        /// en: Disposes the database context and in-memory connection so each test remains isolated and local resources are not leaked.
        /// </summary>
        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
            if (Directory.Exists(_workingDirectory))
                Directory.Delete(_workingDirectory, recursive: true);
        }
    }
}


