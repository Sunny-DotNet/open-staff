using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Agent.Services;
using OpenStaff.ApiServices;
using OpenStaff.Application.Agents.Services;
using OpenStaff.Core.Agents;
using OpenStaff.Dtos;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;

namespace OpenStaff.Tests.Unit;

public class RuntimeProjectionContractsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    /// <summary>
    /// zh-CN: 构建最小化的 SQLite 运行时投影环境，让契约映射测试能够直接覆盖 EF 实体到 DTO 的真实转换逻辑。
    /// en: Builds a minimal SQLite-backed projection environment so the contract-mapping tests exercise the real EF-entity-to-DTO conversion path.
    /// </summary>
    public RuntimeProjectionContractsTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    /// <summary>
    /// zh-CN: 验证任务应用服务会把任务元数据中的运行时字段完整投影到契约 DTO，避免前端丢失会话和重试上下文。
    /// en: Verifies that the task app service projects runtime fields from task metadata into the contract DTO so the client keeps session and retry context.
    /// </summary>
    [Fact]
    public async Task TaskApiService_GetByIdAsync_MapsRuntimeMetadata()
    {
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var frameId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        _db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Task Contract"
        });
        _db.Tasks.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            Title = "编写 API",
            Metadata = System.Text.Json.JsonSerializer.Serialize(new TaskItemRuntimeMetadata
            {
                SessionId = sessionId,
                FrameId = frameId,
                MessageId = messageId,
                Scene = SessionSceneTypes.ProjectGroup,
                Source = "project_group_secretary_dispatch",
                LastStatus = TaskItemStatus.Blocked,
                LastResult = "已输出阶段结果",
                LastError = "缺少 file_system",
                Model = "gpt-4.1",
                AttemptCount = 2,
                TotalTokens = 64,
                DurationMs = 1200,
                FirstTokenMs = 150
            })
        });
        await _db.SaveChangesAsync();

        var service = new TaskApiService(
            new TaskItemRepository(_db),
            new ProjectRepository(_db),
            new TaskDependencyRepository(_db),
            new AgentEventRepository(_db),
            _db,
            null!);
        var result = await service.GetByIdAsync(
            new OpenStaff.Dtos.GetTaskByIdRequest
            {
                ProjectId = projectId,
                TaskId = taskId
            },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(sessionId, result!.SessionId);
        Assert.Equal(frameId, result.FrameId);
        Assert.Equal(messageId, result.MessageId);
        Assert.Equal(SessionSceneTypes.ProjectGroup, result.Scene);
        Assert.Equal("project_group_secretary_dispatch", result.DispatchSource);
        Assert.Equal(TaskItemStatus.Blocked, result.LastStatus);
        Assert.Equal("已输出阶段结果", result.LastResult);
        Assert.Equal("缺少 file_system", result.LastError);
        Assert.Equal("gpt-4.1", result.Model);
        Assert.Equal(2, result.AttemptCount);
        Assert.Equal(64, result.TotalTokens);
        Assert.Equal(1200, result.DurationMs);
        Assert.Equal(150, result.FirstTokenMs);
    }

    /// <summary>
    /// zh-CN: 验证代理事件查询会展开结构化元数据，确保工具调用轨迹能以契约字段形式返回给调用方。
    /// en: Verifies that agent-event queries expand structured metadata so tool-call traces are returned to callers as first-class contract fields.
    /// </summary>
    [Fact]
    public async Task AgentApiService_GetEventsAsync_MapsStructuredRuntimeMetadata()
    {
        var projectId = Guid.NewGuid();
        var agentRoleId = Guid.NewGuid();
        var projectAgentId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var frameId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        _db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Agent Contract"
        });
        _db.AgentRoles.Add(new AgentRole
        {
            Id = agentRoleId,
            Name = "Producer",
            JobTitle = "producer",
            IsActive = true
        });
        _db.ProjectAgentRoles.Add(new ProjectAgentRole
        {
            Id = projectAgentId,
            ProjectId = projectId,
            AgentRoleId = agentRoleId
        });
        _db.AgentEvents.Add(new AgentEvent
        {
            Id = eventId,
            ProjectId = projectId,
            ProjectAgentRoleId = projectAgentId,
            EventType = EventTypes.ToolCall,
            Content = "调用工具：search",
            Metadata = System.Text.Json.JsonSerializer.Serialize(new AgentEventMetadataPayload
            {
                TaskId = taskId,
                SessionId = sessionId,
                FrameId = frameId,
                MessageId = messageId,
                Scene = SessionSceneTypes.ProjectGroup,
                Model = "gpt-4.1",
                ToolName = "search",
                ToolCallId = "call-1",
                Status = "calling",
                Detail = "{\"q\":\"api\"}",
                Attempt = 1,
                TotalTokens = 32,
                DurationMs = 450,
                FirstTokenMs = 80
            })
        });
        await _db.SaveChangesAsync();

        var projectAgentService = new ProjectAgentService(
            new ProjectAgentRoleRepository(_db),
            new AgentEventRepository(_db),
            new Mock<IAgentService>().Object,
            NullLogger<ProjectAgentService>.Instance);
        var apiService = new AgentApiService(
            projectAgentService,
            null!,
            new Mock<IAgentPromptGenerator>().Object,
            new Mock<IAgentMcpToolService>().Object,
            new Mock<IAgentSkillRuntimeService>().Object);

        var result = await apiService.GetEventsAsync(
            new GetAgentEventsRequest
            {
                ProjectId = projectId,
                ProjectAgentRoleId = projectAgentId,
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(eventId, item.Id);
        Assert.Equal(EventTypes.ToolCall, item.EventType);
        Assert.Equal("调用工具：search", item.Content);
        Assert.Equal(taskId, item.TaskId);
        Assert.Equal(sessionId, item.SessionId);
        Assert.Equal(frameId, item.FrameId);
        Assert.Equal(messageId, item.MessageId);
        Assert.Equal(SessionSceneTypes.ProjectGroup, item.Scene);
        Assert.Equal("search", item.ToolName);
        Assert.Equal("call-1", item.ToolCallId);
        Assert.Equal("calling", item.Status);
        Assert.Equal("{\"q\":\"api\"}", item.Detail);
        Assert.Equal(1, item.Attempt);
        Assert.Equal(32, item.TotalTokens);
        Assert.Equal(450, item.DurationMs);
        Assert.Equal(80, item.FirstTokenMs);
    }

    /// <summary>
    /// zh-CN: 释放测试数据库连接，避免跨用例共享内存数据库状态。
    /// en: Releases the test database connection so in-memory SQLite state is not shared across test cases.
    /// </summary>
    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}


