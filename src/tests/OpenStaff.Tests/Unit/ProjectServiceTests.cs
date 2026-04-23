using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenStaff.Application.Conversations.Services;
using OpenStaff.Application.McpServers.Services;
using OpenStaff.Application.Projects.Services;
using OpenStaff.Application.Skills.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Infrastructure.Export;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class ProjectServiceTests
{
    /// <summary>
    /// zh-CN: 验证创建项目时如果选择了默认 Provider 但没选模型，会被后端直接拒绝，避免无效默认模型配置写入数据库。
    /// en: Verifies that project creation is rejected server-side when a default provider is selected without a model, preventing invalid default model settings from being persisted.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ThrowsWhenProviderIsSelectedWithoutModel()
    {
        using var context = new TestContext();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.CreateAsync(new CreateProjectRequest
            {
                Name = "Missing Model",
                Language = "zh-CN",
                DefaultProviderId = Guid.NewGuid()
            }, CancellationToken.None));

        Assert.Contains("必须同时选择默认模型", ex.Message);
    }

    /// <summary>
    /// zh-CN: 验证更新项目时可以显式清空默认 Provider / Model，确保项目设置页保存“取消默认模型”后数据会真正落库。
    /// en: Verifies that project updates can explicitly clear the default provider/model so removing the defaults in project settings is persisted.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ClearsDefaultProviderAndModelWhenExplicitlyNull()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Clear Defaults", ProjectPhases.Brainstorming);
        project.DefaultProviderId = Guid.NewGuid();
        project.DefaultModelName = "gpt-4.1";
        await context.Db.SaveChangesAsync();

        var updated = await context.Service.UpdateAsync(project.Id, new UpdateProjectRequest
        {
            DefaultProviderId = null,
            DefaultModelName = null
        }, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Null(updated!.DefaultProviderId);
        Assert.Null(updated.DefaultModelName);
    }

    /// <summary>
    /// zh-CN: 验证更新项目时如果选了默认 Provider 却没有模型，会返回明确异常，防止绕过前端校验写入不完整配置。
    /// en: Verifies that project updates fail with a clear error when a default provider is supplied without a model, so incomplete settings cannot bypass frontend validation.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ThrowsWhenProviderIsSelectedWithoutModel()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Update Missing Model", ProjectPhases.Brainstorming);
        project.Language = "en-US";
        await context.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.UpdateAsync(project.Id, new UpdateProjectRequest
            {
                DefaultProviderId = Guid.NewGuid(),
                DefaultModelName = null
            }, CancellationToken.None));

        Assert.Contains("default model is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// zh-CN: 验证头脑风暴状态应用会同时更新展示文案、README 内容和项目阶段，覆盖结构化响应的核心落库路径。
    /// en: Verifies that applying brainstorm state updates the display text, README content, and project phase together, covering the main persistence path for structured responses.
    /// </summary>
    [Fact]
    public async Task ApplyBrainstormStateAsync_UpdatesReadmeAndPhase()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Brainstorm Apply", ProjectPhases.Brainstorming);
        var readmePath = Path.Combine(project.WorkspacePath!, ".staff", "project-brainstorm.md");
        Directory.CreateDirectory(Path.GetDirectoryName(readmePath)!);
        await File.WriteAllTextAsync(readmePath, "# Old\n");

        var responseContent = BuildBrainstormResponse(
            "已整理为可启动状态。",
            "# New Doc\n\nUpdated details",
            ProjectPhases.ReadyToStart);

        var result = await context.Service.ApplyBrainstormStateAsync(project.Id, responseContent, CancellationToken.None);

        var updatedProject = await context.Db.Projects.AsNoTracking().SingleAsync(p => p.Id == project.Id);
        var updatedReadme = await File.ReadAllTextAsync(readmePath);

        Assert.Equal("已整理为可启动状态。", result.DisplayContent);
        Assert.True(result.DocumentUpdated);
        Assert.True(result.PhaseChanged);
        Assert.Equal(ProjectPhases.ReadyToStart, result.CurrentPhase);
        Assert.Equal(ProjectPhases.ReadyToStart, updatedProject.Phase);
        Assert.Equal("# New Doc\n\nUpdated details\n", updatedReadme);
    }

    /// <summary>
    /// zh-CN: 验证项目初始化会创建统一的脑暴文档路径，确保后续秘书协作始终围绕 .staff/project-brainstorm.md 展开。
    /// en: Verifies that project initialization creates the unified brainstorm document path so later secretary collaboration always targets .staff/project-brainstorm.md.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_CreatesBrainstormDocumentInStaffFolder()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Brainstorm Init", ProjectPhases.Brainstorming);
        project.Status = ProjectStatus.Initializing;
        context.Db.AgentRoles.Add(new AgentRole
        {
            Name = "Monica",
            JobTitle = BuiltinRoleTypes.Secretary,
            Source = AgentSource.Builtin,
            IsBuiltin = true,
            IsActive = true
        });
        await context.Db.SaveChangesAsync();

        await context.Service.InitializeAsync(project.Id, CancellationToken.None);

        var initializedProject = await context.Db.Projects.AsNoTracking().SingleAsync(p => p.Id == project.Id);
        var brainstormPath = Path.Combine(initializedProject.WorkspacePath!, ".staff", "project-brainstorm.md");

        Assert.True(File.Exists(brainstormPath));
        Assert.DoesNotContain("R.MD", brainstormPath, StringComparison.OrdinalIgnoreCase);
        var content = await File.ReadAllTextAsync(brainstormPath);
        Assert.Contains("# Brainstorm Init 项目头脑风暴", content);
        Assert.Equal(ProjectStatus.Active, initializedProject.Status);
        Assert.Equal(ProjectPhases.Brainstorming, initializedProject.Phase);
    }

    /// <summary>
    /// zh-CN: 验证项目初始化会自动创建脑暴会话和秘书首条消息，确保用户首次打开头脑风暴时就能看到明确的引导。
    /// en: Verifies that project initialization automatically creates a brainstorm session and the secretary's opening message so users see guidance immediately.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_CreatesBrainstormKickoffSessionAndAssistantMessage()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Brainstorm Kickoff", ProjectPhases.Brainstorming);
        project.Status = ProjectStatus.Initializing;
        var secretaryRole = new AgentRole
        {
            Name = "Monica",
            JobTitle = BuiltinRoleTypes.Secretary,
            Source = AgentSource.Builtin,
            IsBuiltin = true,
            IsActive = true
        };
        context.Db.AgentRoles.Add(secretaryRole);
        await context.Db.SaveChangesAsync();

        await context.Service.InitializeAsync(project.Id, CancellationToken.None);

        var session = await context.Db.ChatSessions
            .AsNoTracking()
            .SingleAsync(item => item.ProjectId == project.Id && item.Scene == SessionSceneTypes.ProjectBrainstorm);
        var frame = await context.Db.ChatFrames
            .AsNoTracking()
            .SingleAsync(item => item.SessionId == session.Id);
        var message = await context.Db.ChatMessages
            .AsNoTracking()
            .SingleAsync(item => item.SessionId == session.Id);

        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(ContextStrategies.Full, session.ContextStrategy);
        Assert.Equal(FrameStatus.Completed, frame.Status);
        Assert.Equal(MessageRoles.Assistant, message.Role);
        Assert.Equal(secretaryRole.Id, message.AgentRoleId);
        Assert.NotNull(message.ProjectAgentRoleId);
        Assert.Contains("头脑风暴已经开始了", message.Content);
    }

    /// <summary>
    /// zh-CN: 验证初始化项目时会自动把秘书加入项目成员，避免项目详情页出现“无人入组”的空状态。
    /// en: Verifies that project initialization automatically adds the secretary to the project membership so the project does not start with an empty member list.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_AutoAddsSecretaryToProjectMembers()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Brainstorm Members", ProjectPhases.Brainstorming);
        project.Status = ProjectStatus.Initializing;
        var secretaryRole = new AgentRole
        {
            Name = "Monica",
            JobTitle = BuiltinRoleTypes.Secretary,
            Source = AgentSource.Builtin,
            IsBuiltin = true,
            IsActive = true
        };
        context.Db.AgentRoles.Add(secretaryRole);
        await context.Db.SaveChangesAsync();

        await context.Service.InitializeAsync(project.Id, CancellationToken.None);

        var projectAgents = await context.Db.ProjectAgentRoles
            .AsNoTracking()
            .Where(item => item.ProjectId == project.Id)
            .ToListAsync();

        var projectAgent = Assert.Single(projectAgents);
        Assert.Equal(secretaryRole.Id, projectAgent.AgentRoleId);
    }

    /// <summary>
    /// zh-CN: 验证当项目已经存在脑暴消息时，初始化不会重复插入新的秘书开场白，避免重复初始化造成多条首消息。
    /// en: Verifies that initialization does not insert another secretary kickoff when brainstorm messages already exist.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_DoesNotDuplicateBrainstormKickoffWhenMessagesAlreadyExist()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Brainstorm Existing", ProjectPhases.Brainstorming);
        project.Status = ProjectStatus.Initializing;
        context.Db.AgentRoles.Add(new AgentRole
        {
            Name = "Monica",
            JobTitle = BuiltinRoleTypes.Secretary,
            Source = AgentSource.Builtin,
            IsBuiltin = true,
            IsActive = true
        });
        var session = new ChatSession
        {
            ProjectId = project.Id,
            Scene = SessionSceneTypes.ProjectBrainstorm,
            Status = SessionStatus.Active,
            InitialInput = "existing"
        };
        context.Db.ChatSessions.Add(session);
        await context.Db.SaveChangesAsync();

        var frame = new ChatFrame
        {
            SessionId = session.Id,
            Depth = 0,
            Status = FrameStatus.Completed,
            Purpose = "existing"
        };
        context.Db.ChatFrames.Add(frame);
        await context.Db.SaveChangesAsync();

        context.Db.ChatMessages.Add(new ChatMessage
        {
            SessionId = session.Id,
            FrameId = frame.Id,
            Role = MessageRoles.Assistant,
            Content = "已有开场白",
            SequenceNo = 0
        });
        await context.Db.SaveChangesAsync();

        await context.Service.InitializeAsync(project.Id, CancellationToken.None);

        var sessions = await context.Db.ChatSessions
            .AsNoTracking()
            .Where(item => item.ProjectId == project.Id && item.Scene == SessionSceneTypes.ProjectBrainstorm)
            .ToListAsync();
        var frames = await context.Db.ChatFrames
            .AsNoTracking()
            .Where(item => item.SessionId == session.Id)
            .ToListAsync();
        var messages = await context.Db.ChatMessages
            .AsNoTracking()
            .Where(item => item.SessionId == session.Id)
            .ToListAsync();

        Assert.Single(sessions);
        Assert.Single(frames);
        Assert.Single(messages);
    }

    /// <summary>
    /// zh-CN: 验证项目启动时会结束遗留的头脑风暴会话，并创建一个新的项目群执行会话供后续协作使用。
    /// en: Verifies that starting a project completes leftover brainstorm sessions and creates a fresh project-group execution session for follow-up collaboration.
    /// </summary>
    [Fact]
    public async Task StartAsync_CompletesBrainstormSessionsAndCreatesProjectGroupSession()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Start Project", ProjectPhases.ReadyToStart);
        var secretaryRole = new AgentRole
        {
            Name = "Monica",
            JobTitle = BuiltinRoleTypes.Secretary,
            Source = AgentSource.Builtin,
            IsBuiltin = true,
            IsActive = true
        };
        var producerRole = new AgentRole
        {
            Name = "Ada",
            JobTitle = "producer",
            Source = AgentSource.Custom,
            IsActive = true
        };
        context.Db.AgentRoles.AddRange(secretaryRole, producerRole);

        context.Db.ChatSessions.AddRange(
            new ChatSession
            {
                ProjectId = project.Id,
                Scene = SessionSceneTypes.ProjectBrainstorm,
                Status = SessionStatus.Active,
                InitialInput = "brainstorm-active"
            },
            new ChatSession
            {
                ProjectId = project.Id,
                Scene = SessionSceneTypes.ProjectBrainstorm,
                Status = SessionStatus.AwaitingInput,
                InitialInput = "brainstorm-awaiting"
            });
        context.Db.ProjectAgentRoles.Add(new ProjectAgentRole
        {
            ProjectId = project.Id,
            AgentRoleId = producerRole.Id
        });
        await context.Db.SaveChangesAsync();

        var startedProject = await context.Service.StartAsync(project.Id, CancellationToken.None);

        var updatedProject = await context.Db.Projects.AsNoTracking().SingleAsync(p => p.Id == project.Id);
        var brainstormSessions = await context.Db.ChatSessions
            .Where(s => s.ProjectId == project.Id && s.Scene == SessionSceneTypes.ProjectBrainstorm)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();
        var groupSession = await context.Db.ChatSessions
            .AsNoTracking()
            .SingleAsync(s => s.ProjectId == project.Id && s.Scene == SessionSceneTypes.ProjectGroup);

        Assert.Equal(ProjectPhases.Running, startedProject.Phase);
        Assert.Equal(ProjectPhases.Running, updatedProject.Phase);
        Assert.All(brainstormSessions, session =>
        {
            Assert.Equal(SessionStatus.Completed, session.Status);
            Assert.Equal("ProjectBrainstorm 已结束，项目已进入执行阶段。", session.FinalResult);
            Assert.NotNull(session.CompletedAt);
        });
        Assert.Equal(SessionStatus.Active, groupSession.Status);
        Assert.Equal(ContextStrategies.Hybrid, groupSession.ContextStrategy);
        Assert.Equal("项目已启动，项目群聊已创建。", groupSession.InitialInput);
    }

    /// <summary>
    /// zh-CN: 验证启动项目时如果只有秘书而没有其他成员，会抛出明确异常，避免项目在无人执行的状态下进入运行阶段。
    /// en: Verifies that starting a project fails with a clear exception when the secretary is the only member, preventing a running project with no actual team members.
    /// </summary>
    [Fact]
    public async Task StartAsync_ThrowsWhenOnlySecretaryIsAssigned()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Secretary Only", ProjectPhases.ReadyToStart);
        var secretaryRole = new AgentRole
        {
            Name = "Monica",
            JobTitle = BuiltinRoleTypes.Secretary,
            Source = AgentSource.Builtin,
            IsBuiltin = true,
            IsActive = true
        };
        context.Db.AgentRoles.Add(secretaryRole);
        await context.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.StartAsync(project.Id, CancellationToken.None));

        Assert.Contains("至少一名项目成员", ex.Message);
    }

    /// <summary>
    /// zh-CN: 验证项目启动时会在项目群里落一条秘书 kickoff 消息，作为执行阶段的第一条分工同步。
    /// en: Verifies starting a project seeds a secretary kickoff message in the project group as the first execution-phase assignment update.
    /// </summary>
    [Fact]
    public async Task StartAsync_CreatesProjectGroupKickoffMessage()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Kickoff Project", ProjectPhases.ReadyToStart);
        var secretaryRole = new AgentRole
        {
            Name = "Monica",
            JobTitle = BuiltinRoleTypes.Secretary,
            Source = AgentSource.Builtin,
            IsBuiltin = true,
            IsActive = true
        };
        var producerRole = new AgentRole
        {
            Name = "Ada",
            JobTitle = "producer",
            Description = "负责编码实现",
            IsActive = true
        };
        context.Db.AgentRoles.AddRange(secretaryRole, producerRole);
        await context.Db.SaveChangesAsync();

        context.Db.ProjectAgentRoles.AddRange(
            new ProjectAgentRole
            {
                ProjectId = project.Id,
                AgentRoleId = secretaryRole.Id
            },
            new ProjectAgentRole
            {
                ProjectId = project.Id,
                AgentRoleId = producerRole.Id,
                CurrentTask = "实现核心功能"
            });
        await context.Db.SaveChangesAsync();

        await context.Service.StartAsync(project.Id, CancellationToken.None);

        var groupSession = await context.Db.ChatSessions
            .AsNoTracking()
            .SingleAsync(s => s.ProjectId == project.Id && s.Scene == SessionSceneTypes.ProjectGroup);
        var kickoffMessage = await context.Db.ChatMessages
            .AsNoTracking()
            .SingleAsync(item => item.SessionId == groupSession.Id);

        Assert.Equal(MessageRoles.Assistant, kickoffMessage.Role);
        Assert.Contains("正式进入执行阶段", kickoffMessage.Content);
        Assert.Contains("@Ada", kickoffMessage.Content);
    }

    /// <summary>
    /// zh-CN: 验证设置项目代理时会自动补上秘书角色，确保项目群流程始终保留协调入口。
    /// en: Verifies that assigning project agents automatically adds the secretary role so project-group workflows always retain their coordination entry point.
    /// </summary>
    [Fact]
    public async Task SetProjectAgentsAsync_AlwaysIncludesSecretary()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Locked Secretary", ProjectPhases.Brainstorming);
        var secretaryRole = new AgentRole
        {
            Name = "李四",
            JobTitle = BuiltinRoleTypes.Secretary,
            Source = AgentSource.Builtin,
            IsBuiltin = true,
            IsActive = true
        };
        var producerRole = new AgentRole
        {
            Name = "张三",
            JobTitle = "producer",
            Source = AgentSource.Custom,
            IsActive = true
        };

        context.Db.AgentRoles.AddRange(secretaryRole, producerRole);
        await context.Db.SaveChangesAsync();

        await context.Service.SetProjectAgentsAsync(project.Id, [producerRole.Id], CancellationToken.None);

        var projectAgents = await context.Db.ProjectAgentRoles
            .AsNoTracking()
            .Where(agent => agent.ProjectId == project.Id)
            .Select(agent => agent.AgentRoleId)
            .ToListAsync();

        Assert.Equal(2, projectAgents.Count);
        Assert.Contains(secretaryRole.Id, projectAgents);
        Assert.Contains(producerRole.Id, projectAgents);
    }

    /// <summary>
    /// zh-CN: 验证读取历史项目成员时会自动修复缺失的秘书成员，避免旧项目页面加载后仍然没有默认协调角色。
    /// en: Verifies that reading project members repairs missing secretary membership so legacy projects regain their default coordinator automatically.
    /// </summary>
    [Fact]
    public async Task GetProjectAgentsAsync_AutoRepairsMissingSecretaryMembership()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Legacy Project", ProjectPhases.Running);
        var secretaryRole = new AgentRole
        {
            Name = "Monica",
            JobTitle = BuiltinRoleTypes.Secretary,
            Source = AgentSource.Builtin,
            IsBuiltin = true,
            IsActive = true
        };
        context.Db.AgentRoles.Add(secretaryRole);
        await context.Db.SaveChangesAsync();

        var projectAgents = await context.Service.GetProjectAgentsAsync(project.Id, CancellationToken.None);

        var projectAgent = Assert.Single(projectAgents);
        Assert.Equal(secretaryRole.Id, projectAgent.AgentRoleId);
        Assert.NotNull(projectAgent.AgentRole);
        Assert.Equal(secretaryRole.Id, projectAgent.AgentRole!.Id);
    }

    /// <summary>
    /// zh-CN: 验证当系统中缺少秘书角色时会抛出明确异常，避免项目以无法协调的代理集合启动。
    /// en: Verifies that a clear exception is thrown when the secretary role is missing so a project cannot start with an uncoordinated agent set.
    /// </summary>
    [Fact]
    public async Task SetProjectAgentsAsync_ThrowsWhenSecretaryRoleIsMissing()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Missing Secretary", ProjectPhases.Brainstorming);
        var producerRole = new AgentRole
        {
            Name = "张三",
            JobTitle = "producer",
            Source = AgentSource.Custom,
            IsActive = true
        };

        context.Db.AgentRoles.Add(producerRole);
        await context.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.SetProjectAgentsAsync(project.Id, [producerRole.Id], CancellationToken.None));

        Assert.Contains("Secretary role is not available", ex.Message);
    }

    /// <summary>
    /// zh-CN: 验证新建项目成员时会复制角色级 MCP/Skill 绑定，并把 Filesystem 工作区改写成项目工作区。
    /// en: Verifies that new project members inherit role MCP/skill bindings and that filesystem bindings are rewritten to the project workspace.
    /// </summary>
    [Fact]
    public async Task SetProjectAgentsAsync_CopiesRoleSkillBindingsToProjectAgents()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Capability Copy", ProjectPhases.Brainstorming);
        var secretaryRole = new AgentRole
        {
            Name = "Monica",
            JobTitle = BuiltinRoleTypes.Secretary,
            Source = AgentSource.Builtin,
            IsBuiltin = true,
            IsActive = true
        };
        var engineerRole = new AgentRole
        {
            Name = "Jennifer",
            JobTitle = "软件工程师",
            Source = AgentSource.Custom,
            IsActive = true
        };
        var filesystemServer = new McpServer
        {
            Name = "Filesystem",
            NpmPackage = "@modelcontextprotocol/server-filesystem",
            IsEnabled = true
        };

        context.Db.AgentRoles.AddRange(secretaryRole, engineerRole);
        context.Db.McpServers.Add(filesystemServer);
        await context.Db.SaveChangesAsync();

        context.Db.AgentRoleMcpBindings.Add(new AgentRoleMcpBinding
        {
            AgentRoleId = engineerRole.Id,
            McpServerId = filesystemServer.Id,
            IsEnabled = true
        });
        context.Db.AgentRoleSkillBindings.Add(new AgentRoleSkillBinding
        {
            AgentRoleId = engineerRole.Id,
            SkillInstallKey = "github--awesome-copilot--gh-cli",
            SkillId = "gh-cli",
            Name = "gh-cli",
            DisplayName = "gh-cli",
            Source = "github/awesome-copilot",
            Owner = "github",
            Repo = "awesome-copilot",
            GithubUrl = "https://github.com/github/awesome-copilot",
            IsEnabled = true
        });
        await context.Db.SaveChangesAsync();

        await context.Service.SetProjectAgentsAsync(project.Id, [engineerRole.Id], CancellationToken.None);

        var projectAgent = await context.Db.ProjectAgentRoles
            .AsNoTracking()
            .SingleAsync(agent => agent.ProjectId == project.Id && agent.AgentRoleId == engineerRole.Id);
        var projectSkillBinding = await context.Db.ProjectAgentRoleSkillBindings
            .AsNoTracking()
            .SingleAsync(binding => binding.ProjectAgentRoleId == projectAgent.Id);

        Assert.Single(await context.Db.AgentRoleMcpBindings
            .AsNoTracking()
            .Where(binding => binding.AgentRoleId == engineerRole.Id)
            .ToListAsync());
        Assert.Equal("github--awesome-copilot--gh-cli", projectSkillBinding.SkillInstallKey);
        Assert.Equal("gh-cli", projectSkillBinding.SkillId);
    }

    /// <summary>
    /// zh-CN: 验证删除项目会清理会话、任务、依赖、事件和检查点等整张关联图，防止孤儿数据残留。
    /// en: Verifies that deleting a project cleans up the full graph of sessions, tasks, dependencies, events, and checkpoints so no orphaned data remains.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_RemovesProjectWithNestedSessionTaskAndEventGraphs()
    {
        using var context = new TestContext();
        var project = await context.AddProjectAsync("Delete Graph", ProjectPhases.Brainstorming);

        var role = new AgentRole
        {
            Name = "李四",
            JobTitle = BuiltinRoleTypes.Secretary,
            Source = AgentSource.Builtin,
            IsBuiltin = true,
            IsActive = true
        };
        context.Db.AgentRoles.Add(role);
        await context.Db.SaveChangesAsync();

        var projectAgent = new ProjectAgentRole
        {
            ProjectId = project.Id,
            AgentRoleId = role.Id
        };
        context.Db.ProjectAgentRoles.Add(projectAgent);

        var parentTask = new TaskItem
        {
            ProjectId = project.Id,
            Title = "Parent Task"
        };
        context.Db.Tasks.Add(parentTask);
        await context.Db.SaveChangesAsync();

        var childTask = new TaskItem
        {
            ProjectId = project.Id,
            Title = "Child Task",
            ParentTaskId = parentTask.Id
        };
        context.Db.Tasks.Add(childTask);

        var session = new ChatSession
        {
            ProjectId = project.Id,
            Scene = SessionSceneTypes.ProjectBrainstorm,
            Status = SessionStatus.Active,
            InitialInput = "brainstorm"
        };
        context.Db.ChatSessions.Add(session);
        await context.Db.SaveChangesAsync();

        var rootFrame = new ChatFrame
        {
            SessionId = session.Id,
            Depth = 0,
            Status = FrameStatus.Completed,
            Purpose = "root"
        };
        context.Db.ChatFrames.Add(rootFrame);
        await context.Db.SaveChangesAsync();

        var childFrame = new ChatFrame
        {
            SessionId = session.Id,
            ParentFrameId = rootFrame.Id,
            Depth = 1,
            Status = FrameStatus.Completed,
            Purpose = "child"
        };
        context.Db.ChatFrames.Add(childFrame);
        await context.Db.SaveChangesAsync();

        var userMessage = new ChatMessage
        {
            SessionId = session.Id,
            FrameId = rootFrame.Id,
            Role = MessageRoles.User,
            Content = "需求",
            SequenceNo = 0
        };
        context.Db.ChatMessages.Add(userMessage);
        await context.Db.SaveChangesAsync();

        var assistantMessage = new ChatMessage
        {
            SessionId = session.Id,
            FrameId = rootFrame.Id,
            ParentMessageId = userMessage.Id,
            Role = MessageRoles.Assistant,
            Content = "已记录",
            SequenceNo = 1
        };
        context.Db.ChatMessages.Add(assistantMessage);
        context.Db.SessionEvents.Add(new SessionEvent
        {
            SessionId = session.Id,
            EventType = SessionEventTypes.Message,
            SequenceNo = 1,
            MessageId = assistantMessage.Id
        });
        context.Db.TaskDependencies.Add(new TaskDependency
        {
            TaskId = childTask.Id,
            DependsOnId = parentTask.Id
        });
        context.Db.AgentEvents.AddRange(
            new AgentEvent
            {
                ProjectId = project.Id,
                ProjectAgentRoleId = projectAgent.Id,
                EventType = EventTypes.Message,
                Content = "root event"
            },
            new AgentEvent
            {
                ProjectId = project.Id,
                ProjectAgentRoleId = projectAgent.Id,
                EventType = EventTypes.Message,
                Content = "child event"
            });
        context.Db.Checkpoints.Add(new Checkpoint
        {
            ProjectId = project.Id,
            TaskId = childTask.Id,
            ProjectAgentRoleId = projectAgent.Id,
            Description = "checkpoint"
        });
        await context.Db.SaveChangesAsync();

        var agentEvents = await context.Db.AgentEvents
            .Where(agentEvent => agentEvent.ProjectId == project.Id)
            .OrderBy(agentEvent => agentEvent.CreatedAt)
            .ToListAsync();
        agentEvents[1].ParentEventId = agentEvents[0].Id;
        await context.Db.SaveChangesAsync();

        var deleted = await context.Service.DeleteAsync(project.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.False(await context.Db.Projects.AnyAsync(item => item.Id == project.Id));
        Assert.False(await context.Db.ChatSessions.AnyAsync(item => item.ProjectId == project.Id));
        Assert.False(await context.Db.ChatFrames.AnyAsync(item => item.SessionId == session.Id));
        Assert.False(await context.Db.ChatMessages.AnyAsync(item => item.SessionId == session.Id));
        Assert.False(await context.Db.SessionEvents.AnyAsync(item => item.SessionId == session.Id));
        Assert.False(await context.Db.Tasks.AnyAsync(item => item.ProjectId == project.Id));
        Assert.False(await context.Db.TaskDependencies.AnyAsync());
        Assert.False(await context.Db.AgentEvents.AnyAsync(item => item.ProjectId == project.Id));
        Assert.False(await context.Db.ProjectAgentRoles.AnyAsync(item => item.ProjectId == project.Id));
        Assert.False(await context.Db.Checkpoints.AnyAsync(item => item.ProjectId == project.Id));
    }

    /// <summary>
    /// zh-CN: 组装带有结构化状态标签的头脑风暴响应，确保测试使用与生产解析器一致的封装格式。
    /// en: Builds a brainstorm response wrapped in the structured state tag so the tests exercise the same envelope format that production parsing expects.
    /// </summary>
    private static string BuildBrainstormResponse(string message, string documentMarkdown, string phase)
    {
        var envelope = System.Text.Json.JsonSerializer.Serialize(new
        {
            documentMarkdown,
            phase
        });

        return $"{message}\n\n<openstaff_brainstorm_state>{envelope}</openstaff_brainstorm_state>";
    }

    private sealed class TestContext : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly string _workspaceRoot;

        /// <summary>
        /// zh-CN: 初始化隔离的内存数据库与工作区目录，为 ProjectService 测试提供接近真实环境的依赖组合。
        /// en: Initializes an isolated in-memory database and workspace root to give the ProjectService tests a near-production dependency setup.
        /// </summary>
        public TestContext()
        {
            _workspaceRoot = Path.Combine(Path.GetTempPath(), "openstaff-project-service-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workspaceRoot);

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            Services = new ServiceCollection()
                .AddLogging()
                .AddDbContext<AppDbContext>(options => options.UseSqlite(_connection))
                .BuildServiceProvider();

            Db = Services.GetRequiredService<AppDbContext>();
            Db.Database.EnsureCreated();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Workspaces:RootPath"] = _workspaceRoot
                })
                .Build();
            var managedSkillStore = new Mock<IManagedSkillStore>();
            managedSkillStore
                .Setup(store => store.GetInstalledAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<ManagedInstalledSkill>());
            var capabilityBindingService = new RoleCapabilityBindingService(
                new AgentRoleRepository(Db),
                new ProjectAgentRoleRepository(Db),
                new McpServerRepository(Db),
                new AgentRoleMcpBindingRepository(Db),
                new AgentRoleSkillBindingRepository(Db),
                new ProjectAgentRoleSkillBindingRepository(Db),
                managedSkillStore.Object,
                Db,
                NullLogger<RoleCapabilityBindingService>.Instance);
            var conversationTriggerService = new ConversationTriggerService(
                new ChatSessionRepository(Db),
                new ChatFrameRepository(Db),
                new ChatMessageRepository(Db),
                new ProjectAgentRoleRepository(Db),
                new AgentRoleRepository(Db),
                Db,
                NullLogger<ConversationTriggerService>.Instance);

            Service = new ProjectService(
                new ProjectRepository(Db),
                new ProjectAgentRoleRepository(Db),
                new AgentRoleRepository(Db),
                new TaskItemRepository(Db),
                new TaskDependencyRepository(Db),
                new AgentEventRepository(Db),
                new CheckpointRepository(Db),
                new ChatSessionRepository(Db),
                new ChatFrameRepository(Db),
                new ChatMessageRepository(Db),
                new SessionEventRepository(Db),
                Db,
                new ProjectExporter(new ProjectRepository(Db), NullLogger<ProjectExporter>.Instance),
                new ProjectImporter(new ProjectRepository(Db), Db, NullLogger<ProjectImporter>.Instance),
                config,
                conversationTriggerService,
                capabilityBindingService,
                NullLogger<ProjectService>.Instance,
                Microsoft.Extensions.Options.Options.Create(new OpenStaff.Options.OpenStaffOptions { WorkingDirectory = _workspaceRoot }));
        }

        public ServiceProvider Services { get; }
        public AppDbContext Db { get; }
        public ProjectService Service { get; }

        /// <summary>
        /// zh-CN: 创建带独立工作区的最小项目记录，便于各测试专注验证项目服务行为而无需重复样板初始化。
        /// en: Creates a minimal project record with its own workspace so each test can focus on ProjectService behavior without repeating boilerplate setup.
        /// </summary>
        public async Task<Project> AddProjectAsync(string name, string phase)
        {
            var project = new Project
            {
                Name = name,
                Status = ProjectStatus.Active,
                Phase = phase,
                Language = "zh-CN",
                WorkspacePath = Path.Combine(_workspaceRoot, Guid.NewGuid().ToString("N"))
            };
            Directory.CreateDirectory(project.WorkspacePath);
            Db.Projects.Add(project);
            await Db.SaveChangesAsync();
            return project;
        }

        /// <summary>
        /// zh-CN: 释放数据库和测试工作区，确保文件系统副作用不会泄漏到其他用例。
        /// en: Disposes the database and test workspace so file-system side effects do not leak into other test cases.
        /// </summary>
        public void Dispose()
        {
            Db.Dispose();
            Services.Dispose();
            _connection.Dispose();

            if (Directory.Exists(_workspaceRoot))
            {
                Directory.Delete(_workspaceRoot, recursive: true);
            }
        }
    }
}

