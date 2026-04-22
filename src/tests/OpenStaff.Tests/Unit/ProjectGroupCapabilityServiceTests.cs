using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenStaff.Agent.Services;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Application.Sessions.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Repositories;

namespace OpenStaff.Tests.Unit;

public class ProjectGroupCapabilityServiceTests
{
    /// <summary>
    /// zh-CN: 验证所需工具可通过 MCP 能力授予满足时，服务会允许重试并失效运行时缓存。
    /// en: Verifies that when the required tool can be granted through MCP capabilities, the service allows a retry and invalidates the runtime cache.
    /// </summary>
    [Fact]
    public async Task TryPrepareCapabilityRetryAsync_WithSatisfiedMcpTool_AllowsRetryAndInvalidatesCache()
    {
        using var context = new TestContext(["file_system"], [], capabilityChanged: true);
        var project = await context.AddProjectAsync();
        var setup = await context.AddTaskContextAsync(project.Id, "producer");

        var result = await context.Service.TryPrepareCapabilityRetryAsync(
            project.Id,
            setup.Frame.Id,
            new ProjectGroupCapabilityRequest { RequiredTools = ["file_system"] },
            CancellationToken.None);

        var role = await context.Db.AgentRoles.AsNoTracking().SingleAsync(item => item.Id == setup.Role.Id);
        var capabilityEvent = await context.Db.AgentEvents.AsNoTracking()
            .FirstOrDefaultAsync(item => item.EventType == EventTypes.CapabilityApproved);

        Assert.True(result.CanRetryWithoutPenalty);
        Assert.Empty(result.MissingTools);
        Assert.DoesNotContain("file_system", role.Config ?? string.Empty);
        Assert.NotNull(capabilityEvent);
        Assert.Equal(project.Id, context.RuntimeCache.ProjectId);
        Assert.Equal("producer", context.RuntimeCache.RoleType);
    }

    /// <summary>
    /// zh-CN: 验证所需工具不存在时，服务会返回缺失列表且不会误触发缓存失效或重试授权。
    /// en: Verifies that when the required tool is unavailable, the service returns the missing-tool list and does not incorrectly trigger cache invalidation or retry approval.
    /// </summary>
    [Fact]
    public async Task TryPrepareCapabilityRetryAsync_WithMissingTool_ReturnsFailure()
    {
        using var context = new TestContext([], ["file_system"], capabilityChanged: false);
        var project = await context.AddProjectAsync();
        var setup = await context.AddTaskContextAsync(project.Id, "producer");

        var result = await context.Service.TryPrepareCapabilityRetryAsync(
            project.Id,
            setup.Frame.Id,
            new ProjectGroupCapabilityRequest { RequiredTools = ["file_system"] },
            CancellationToken.None);

        Assert.False(result.CanRetryWithoutPenalty);
        Assert.Equal(["file_system"], result.MissingTools);
        Assert.Null(context.RuntimeCache.ProjectId);
        Assert.Null(context.RuntimeCache.RoleType);
    }

    private sealed class TestContext : IDisposable
    {
        private readonly SqliteConnection _connection;

        /// <summary>
        /// zh-CN: 搭建内存数据库和依赖注入容器，并通过 MCP 能力授予结果隔离服务行为。
        /// en: Sets up the in-memory database and dependency container while isolating service behavior through mocked MCP capability grants.
        /// </summary>
        public TestContext(
            IReadOnlyList<string> satisfiedMcpTools,
            IReadOnlyList<string> missingMcpTools,
            bool capabilityChanged)
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var mcpToolService = new Mock<IAgentMcpToolService>();
            mcpToolService
                .Setup(service => service.EnsureToolsAllowedAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, IReadOnlyCollection<string> requiredTools, CancellationToken _) =>
                    new AgentMcpCapabilityGrantResult(satisfiedMcpTools, missingMcpTools, capabilityChanged));

            RuntimeCache = new FakeRuntimeCache();

            Services = new ServiceCollection()
                .AddLogging()
                .AddSingleton(mcpToolService.Object)
                .AddSingleton<IProjectAgentRuntimeCache>(RuntimeCache)
                .AddDbContext<AppDbContext>(options => options.UseSqlite(_connection))
                .AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>())
                .AddScoped<IChatFrameRepository, ChatFrameRepository>()
                .AddScoped<ITaskItemRepository, TaskItemRepository>()
                .AddScoped<IAgentEventRepository, AgentEventRepository>()
                .BuildServiceProvider();

            Db = Services.GetRequiredService<AppDbContext>();
            Db.Database.EnsureCreated();

            Service = new ProjectGroupCapabilityService(
                Services.GetRequiredService<IServiceScopeFactory>(),
                Services.GetRequiredService<IAgentMcpToolService>(),
                RuntimeCache,
                NullLogger<ProjectGroupCapabilityService>.Instance);
        }

        public ServiceProvider Services { get; }
        public AppDbContext Db { get; }
        public ProjectGroupCapabilityService Service { get; }
        public FakeRuntimeCache RuntimeCache { get; }

        /// <summary>
        /// zh-CN: 创建一个处于执行阶段的最小项目，为能力补齐测试提供真实的项目上下文。
        /// en: Creates a minimal running project that gives the capability-retry tests a realistic project context.
        /// </summary>
        public async Task<Project> AddProjectAsync()
        {
            var project = new Project
            {
                Name = "Capability Project",
                Status = ProjectStatus.Active,
                Phase = ProjectPhases.Running,
                Language = "zh-CN"
            };

            Db.Projects.Add(project);
            await Db.SaveChangesAsync();
            return project;
        }

        /// <summary>
        /// zh-CN: 建立角色、项目代理、会话、任务和帧之间的关联，模拟等待能力补齐的执行现场。
        /// en: Creates the linked role, project-agent, session, task, and frame records that simulate an execution context waiting for capability recovery.
        /// </summary>
        public async Task<(AgentRole Role, ProjectAgentRole Agent, TaskItem Task, ChatFrame Frame)> AddTaskContextAsync(Guid projectId, string roleType)
        {
            var role = new AgentRole
            {
                Name = roleType,
                JobTitle = roleType,
                ProviderType = "builtin",
                IsActive = true
            };
            Db.AgentRoles.Add(role);

            var projectAgent = new ProjectAgentRole
            {
                ProjectId = projectId,
                AgentRole = role,
                Status = AgentStatus.Working,
                CurrentTask = "blocked task"
            };
            Db.ProjectAgentRoles.Add(projectAgent);

            var session = new ChatSession
            {
                ProjectId = projectId,
                Scene = SessionSceneTypes.ProjectGroup,
                Status = SessionStatus.Active,
                InitialInput = "session"
            };
            Db.ChatSessions.Add(session);

            var task = new TaskItem
            {
                ProjectId = projectId,
                Title = "blocked task",
                AssignedProjectAgentRole = projectAgent,
                Status = TaskItemStatus.InProgress
            };
            Db.Tasks.Add(task);

            var frame = new ChatFrame
            {
                Session = session,
                Purpose = "do work",
                Depth = 0,
                Status = FrameStatus.Active,
                Task = task
            };
            Db.ChatFrames.Add(frame);

            await Db.SaveChangesAsync();
            return (role, projectAgent, task, frame);
        }

        /// <summary>
        /// zh-CN: 释放测试使用的数据库连接和服务容器，防止内存数据库状态在用例之间串联。
        /// en: Releases the database connection and service provider used by the test so in-memory state does not bleed across cases.
        /// </summary>
        public void Dispose()
        {
            Db.Dispose();
            Services.Dispose();
            _connection.Dispose();
        }
    }

    private sealed class FakeRuntimeCache : IProjectAgentRuntimeCache
    {
        public Guid? ProjectId { get; private set; }
        public string? RoleType { get; private set; }

        /// <summary>
        /// zh-CN: 记录缓存失效请求，便于断言能力补齐后系统是否要求重新装载代理运行时。
        /// en: Records cache invalidation requests so the tests can assert whether capability recovery asks the system to reload agent runtime state.
        /// </summary>
        public void InvalidateProjectAgent(Guid projectId, string roleType)
        {
            ProjectId = projectId;
            RoleType = roleType;
        }
    }

}

