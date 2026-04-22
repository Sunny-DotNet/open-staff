using Microsoft.Agents.AI;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenStaff.Agent;
using OpenStaff.Agents;
using OpenStaff.Agent.Services;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Agent.Vendor.GitHubCopilot;
using OpenStaff.Dtos;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Repositories;

namespace OpenStaff.Tests.Unit;

public class ApplicationAgentRunFactoryTests
{
    /// <summary>
    /// zh-CN: 验证项目会话按目标角色准备运行时，会恢复上一轮对话谱系，并使用项目默认 Provider 解析模型配置。
    /// en: Verifies preparing a project-targeted run restores prior conversation lineage and resolves model configuration from the project's default provider.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_WithProjectTargetRole_RestoresLineageAndUsesProjectDefaultProvider()
    {
        var providerId = Guid.NewGuid();
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var providerResolverMock = new Mock<IProviderResolver>();
        providerResolverMock
            .Setup(item => item.ResolveAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResolvedProvider());

        var mcpToolServiceMock = new Mock<IAgentMcpToolService>();
        mcpToolServiceMock
            .Setup(item => item.LoadEnabledToolsAsync(It.IsAny<AgentMcpToolLoadContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var promptGeneratorMock = new Mock<IAgentPromptGenerator>();
        promptGeneratorMock
            .Setup(item => item.PromptBuildAsync(It.IsAny<AgentRole>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentRole role, AgentContext _, CancellationToken _) => $"prompt::{role.Name}");
        var fakeProvider = new CaptureAgentProvider();
        var agentFactory = new AgentFactory([fakeProvider]);

        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IAgentRoleRepository>(sp => new AgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectAgentRoleRepository>(sp => new ProjectAgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectRepository>(sp => new ProjectRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IChatMessageRepository>(sp => new ChatMessageRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProviderResolver>(_ => providerResolverMock.Object)
            .BuildServiceProvider();

        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userMessageId = Guid.NewGuid();
        var assistantMessageId = Guid.NewGuid();
        var rootFrameId = Guid.NewGuid();

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            var role = new AgentRole
            {
                Id = roleId,
                Name = "开发工程师",
                JobTitle = "producer",
                ProviderType = "fake",
                ModelProviderId = providerId,
                ModelName = "gpt-4o-mini",
                IsActive = true
            };

            var project = new Project
            {
                Id = projectId,
                Name = "Flight Control",
                DefaultProviderId = providerId,
                Language = "zh-CN"
            };

            var projectAgent = new ProjectAgentRole
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                AgentRoleId = roleId,
                AgentRole = role,
                Project = project
            };

            db.AgentRoles.Add(role);
            db.Projects.Add(project);
            db.ProjectAgentRoles.Add(projectAgent);
            db.ChatSessions.Add(new ChatSession
            {
                Id = sessionId,
                ProjectId = projectId,
                InitialInput = "先做登录页",
                Scene = SessionSceneTypes.ProjectGroup
            });
            db.ChatFrames.Add(new ChatFrame
            {
                Id = rootFrameId,
                SessionId = sessionId,
                Depth = 0,
                Purpose = "先做登录页"
            });
            db.ChatMessages.AddRange(
                new OpenStaff.Entities.ChatMessage
                {
                    Id = userMessageId,
                    SessionId = sessionId,
                    FrameId = rootFrameId,
                    Role = MessageRoles.User,
                    Content = "先做登录页"
                },
                new OpenStaff.Entities.ChatMessage
                {
                    Id = assistantMessageId,
                    SessionId = sessionId,
                    FrameId = rootFrameId,
                    ParentMessageId = userMessageId,
                    Role = MessageRoles.Assistant,
                    Content = "我先拆解任务"
                });

            await db.SaveChangesAsync();
        }

        var factory = new ApplicationAgentRunFactory(
            services.GetRequiredService<IServiceScopeFactory>(),
            agentFactory,
            promptGeneratorMock.Object,
            mcpToolServiceMock.Object,
            new StubGitHubCopilotSessionManager(),
            services.GetRequiredService<ILogger<ApplicationAgentRunFactory>>());

        var prepared = await factory.PrepareAsync(
            new CreateMessageRequest(
                Scene: MessageScene.ProjectGroup,
                MessageContext: new MessageContext(
                    ProjectId: projectId,
                    SessionId: sessionId,
                    ParentMessageId: assistantMessageId,
                    FrameId: Guid.NewGuid(),
                    ParentFrameId: null,
                    TaskId: null,
                    ProjectAgentRoleId: null,
                    TargetRole: "producer",
                    InitiatorRole: "secretary",
                    Extra: new Dictionary<string, string>
                    {
                        ["openstaff_original_input"] = "@Ada 把登录接口也一起补上",
                        ["openstaff_execution_purpose"] = "把登录接口也一起补上",
                        ["openstaff_dispatch_source"] = "project_group_member_dispatch",
                        ["openstaff_dispatch_context"] = "Another project member finished the current stage and handed the follow-up work to you."
                    }),
                InputRole: ChatRole.User,
                Input: "把登录接口也一起补上"),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal("producer", prepared.AgentRole);
        Assert.Equal(3, prepared.Messages.Count);
        Assert.Equal("先做登录页", prepared.Messages[0].Text);
        Assert.Equal("我先拆解任务", prepared.Messages[1].Text);
        Assert.Equal("把登录接口也一起补上", prepared.Messages[2].Text);
        Assert.Equal("producer", fakeProvider.LastRole?.JobTitle);
        Assert.Equal(projectId, fakeProvider.LastContext?.ProjectId);
        Assert.Equal("@Ada 把登录接口也一起补上", fakeProvider.LastContext?.ExtraConfig["openstaff_original_input"]?.ToString());
        Assert.Equal("project_group_member_dispatch", fakeProvider.LastContext?.ExtraConfig["openstaff_dispatch_source"]?.ToString());
        Assert.Equal("Another project member finished the current stage and handed the follow-up work to you.", fakeProvider.LastContext?.ExtraConfig["openstaff_dispatch_context"]?.ToString());
    }

    /// <summary>
    /// zh-CN: 验证头脑风暴场景中的秘书角色即使带有内置默认模型，也会优先采用项目级默认模型。
    /// en: Verifies the secretary role in brainstorm mode prefers the project-level default model even when the builtin role carries its own default.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_ProjectBrainstormSecretary_UsesProjectBrainstormModelWhenRoleOnlyHasBuiltinDefault()
    {
        var providerId = Guid.NewGuid();
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var providerResolverMock = new Mock<IProviderResolver>();
        providerResolverMock
            .Setup(item => item.ResolveAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResolvedProvider());

        var mcpToolServiceMock = new Mock<IAgentMcpToolService>();
        mcpToolServiceMock
            .Setup(item => item.LoadEnabledToolsAsync(It.IsAny<AgentMcpToolLoadContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var promptGeneratorMock = new Mock<IAgentPromptGenerator>();
        promptGeneratorMock
            .Setup(item => item.PromptBuildAsync(It.IsAny<AgentRole>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentRole role, AgentContext _, CancellationToken _) => $"prompt::{role.Name}");

        var fakeProvider = new CaptureAgentProvider();
        var agentFactory = new AgentFactory([fakeProvider]);

        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IAgentRoleRepository>(sp => new AgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectAgentRoleRepository>(sp => new ProjectAgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectRepository>(sp => new ProjectRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IChatMessageRepository>(sp => new ChatMessageRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProviderResolver>(_ => providerResolverMock.Object)
            .BuildServiceProvider();

        var projectId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        const string projectBrainstormModel = "gpt-5.4-mini";

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.AgentRoles.Add(new AgentRole
            {
                Id = roleId,
                Name = "李四",
                JobTitle = BuiltinRoleTypes.Secretary,
                ProviderType = "fake",
                ModelProviderId = providerId,
                ModelName = "gpt-4o-mini",
                Source = AgentSource.Builtin,
                IsBuiltin = true,
                IsActive = true
            });
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Brainstorm Model Project",
                DefaultProviderId = providerId,
                DefaultModelName = projectBrainstormModel,
                Language = "zh-CN"
            });
            await db.SaveChangesAsync();
        }

        var factory = new ApplicationAgentRunFactory(
            services.GetRequiredService<IServiceScopeFactory>(),
            agentFactory,
            promptGeneratorMock.Object,
            mcpToolServiceMock.Object,
            new StubGitHubCopilotSessionManager(),
            services.GetRequiredService<ILogger<ApplicationAgentRunFactory>>());

        var prepared = await factory.PrepareAsync(
            new CreateMessageRequest(
                Scene: MessageScene.ProjectBrainstorm,
                MessageContext: new MessageContext(
                    ProjectId: projectId,
                    SessionId: null,
                    ParentMessageId: null,
                    FrameId: null,
                    ParentFrameId: null,
                    TaskId: null,
                    ProjectAgentRoleId: null,
                    TargetRole: BuiltinRoleTypes.Secretary,
                    InitiatorRole: MessageRoles.User,
                    Extra: null),
                InputRole: ChatRole.User,
                Input: "帮我梳理需求"),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal(BuiltinRoleTypes.Secretary, prepared.AgentRole);
        Assert.Equal("gpt-4o-mini", prepared.Model);
        Assert.Equal("gpt-4o-mini", fakeProvider.LastRole?.ModelName);
    }

    /// <summary>
    /// zh-CN: 验证项目头脑风暴的默认模型和 Provider 会覆盖目录中秘书角色的静态配置，保证项目级定制优先。
    /// en: Verifies brainstorm defaults from the project override the catalog secretary configuration so project-specific settings win.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_ProjectBrainstormSecretary_OverridesCatalogSecretaryConfiguration()
    {
        var projectProviderId = Guid.NewGuid();
        var catalogProviderId = Guid.NewGuid();
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var providerResolverMock = new Mock<IProviderResolver>();
        providerResolverMock
            .Setup(item => item.ResolveAsync(projectProviderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResolvedProvider());
        providerResolverMock
            .Setup(item => item.ResolveAsync(catalogProviderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResolvedProvider());

        var mcpToolServiceMock = new Mock<IAgentMcpToolService>();
        mcpToolServiceMock
            .Setup(item => item.LoadEnabledToolsAsync(It.IsAny<AgentMcpToolLoadContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var promptGeneratorMock = new Mock<IAgentPromptGenerator>();
        promptGeneratorMock
            .Setup(item => item.PromptBuildAsync(It.IsAny<AgentRole>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentRole role, AgentContext _, CancellationToken _) => $"prompt::{role.Name}");

        var fakeProvider = new CaptureAgentProvider();
        var agentFactory = new AgentFactory([fakeProvider]);

        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IAgentRoleRepository>(sp => new AgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectAgentRoleRepository>(sp => new ProjectAgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectRepository>(sp => new ProjectRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IChatMessageRepository>(sp => new ChatMessageRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProviderResolver>(_ => providerResolverMock.Object)
            .BuildServiceProvider();

        var projectId = Guid.NewGuid();
        const string projectBrainstormModel = "gpt-5.4-mini";
        const string catalogSecretaryModel = "glm-5";

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.AgentRoles.Add(new AgentRole
            {
                Id = Guid.NewGuid(),
                Name = "李四",
                JobTitle = BuiltinRoleTypes.Secretary,
                ProviderType = "fake",
                ModelProviderId = catalogProviderId,
                ModelName = catalogSecretaryModel,
                Source = AgentSource.Builtin,
                IsBuiltin = true,
                IsActive = true
            });
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Project Brainstorm Override",
                DefaultProviderId = projectProviderId,
                DefaultModelName = projectBrainstormModel,
                Language = "zh-CN"
            });
            await db.SaveChangesAsync();
        }

        var factory = new ApplicationAgentRunFactory(
            services.GetRequiredService<IServiceScopeFactory>(),
            agentFactory,
            promptGeneratorMock.Object,
            mcpToolServiceMock.Object,
            new StubGitHubCopilotSessionManager(),
            services.GetRequiredService<ILogger<ApplicationAgentRunFactory>>());

        var prepared = await factory.PrepareAsync(
            new CreateMessageRequest(
                Scene: MessageScene.ProjectBrainstorm,
                MessageContext: new MessageContext(
                    ProjectId: projectId,
                    SessionId: null,
                    ParentMessageId: null,
                    FrameId: null,
                    ParentFrameId: null,
                    TaskId: null,
                    ProjectAgentRoleId: null,
                    TargetRole: BuiltinRoleTypes.Secretary,
                    InitiatorRole: MessageRoles.User,
                    Extra: null),
                InputRole: ChatRole.User,
                Input: "继续整理需求"),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal(catalogSecretaryModel, prepared.Model);
        Assert.Equal(catalogSecretaryModel, fakeProvider.LastRole?.ModelName);
        Assert.Equal(catalogProviderId, fakeProvider.LastRole?.ModelProviderId);
    }

    /// <summary>
    /// zh-CN: 验证实时传入的模型覆盖配置优先级高于项目默认值，避免用户临时调整被项目设置吞掉。
    /// en: Verifies live per-request model overrides take precedence over project defaults so temporary user choices are preserved.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_ProjectBrainstormSecretary_PreservesLiveOverrideOverProjectDefaults()
    {
        var projectProviderId = Guid.NewGuid();
        var liveOverrideProviderId = Guid.NewGuid();
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var providerResolverMock = new Mock<IProviderResolver>();
        providerResolverMock
            .Setup(item => item.ResolveAsync(projectProviderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResolvedProvider());
        providerResolverMock
            .Setup(item => item.ResolveAsync(liveOverrideProviderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResolvedProvider());

        var mcpToolServiceMock = new Mock<IAgentMcpToolService>();
        mcpToolServiceMock
            .Setup(item => item.LoadEnabledToolsAsync(It.IsAny<AgentMcpToolLoadContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var promptGeneratorMock = new Mock<IAgentPromptGenerator>();
        promptGeneratorMock
            .Setup(item => item.PromptBuildAsync(It.IsAny<AgentRole>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentRole role, AgentContext _, CancellationToken _) => $"prompt::{role.Name}");

        var fakeProvider = new CaptureAgentProvider();
        var agentFactory = new AgentFactory([fakeProvider]);

        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IAgentRoleRepository>(sp => new AgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectAgentRoleRepository>(sp => new ProjectAgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectRepository>(sp => new ProjectRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IChatMessageRepository>(sp => new ChatMessageRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProviderResolver>(_ => providerResolverMock.Object)
            .BuildServiceProvider();

        var projectId = Guid.NewGuid();
        const string liveOverrideModel = "override-live-model";

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.AgentRoles.Add(new AgentRole
            {
                Id = Guid.NewGuid(),
                Name = "李四",
                JobTitle = BuiltinRoleTypes.Secretary,
                ProviderType = "fake",
                ModelProviderId = Guid.NewGuid(),
                ModelName = "glm-5",
                Source = AgentSource.Builtin,
                IsBuiltin = true,
                IsActive = true
            });
            db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Live Override Project",
                DefaultProviderId = projectProviderId,
                DefaultModelName = "gpt-5.4-mini",
                Language = "zh-CN"
            });
            await db.SaveChangesAsync();
        }

        var factory = new ApplicationAgentRunFactory(
            services.GetRequiredService<IServiceScopeFactory>(),
            agentFactory,
            promptGeneratorMock.Object,
            mcpToolServiceMock.Object,
            new StubGitHubCopilotSessionManager(),
            services.GetRequiredService<ILogger<ApplicationAgentRunFactory>>());

        var overrideJson = $$"""
            {
              "modelProviderId": "{{liveOverrideProviderId}}",
              "modelName": "{{liveOverrideModel}}"
            }
            """;

        var prepared = await factory.PrepareAsync(
            new CreateMessageRequest(
                Scene: MessageScene.ProjectBrainstorm,
                MessageContext: new MessageContext(
                    ProjectId: projectId,
                    SessionId: null,
                    ParentMessageId: null,
                    FrameId: null,
                    ParentFrameId: null,
                    TaskId: null,
                    ProjectAgentRoleId: null,
                    TargetRole: BuiltinRoleTypes.Secretary,
                    InitiatorRole: MessageRoles.User,
                    Extra: null),
                InputRole: ChatRole.User,
                Input: "继续整理需求",
                OverrideJson: overrideJson),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal(liveOverrideModel, prepared.Model);
        Assert.Equal(liveOverrideModel, fakeProvider.LastRole?.ModelName);
        Assert.Equal(liveOverrideProviderId, fakeProvider.LastRole?.ModelProviderId);
    }

    /// <summary>
    /// zh-CN: 验证子 Frame 绑定项目代理时，会跨 Frame 恢复祖先消息谱系，并把正确的代理实例标识传入运行上下文。
    /// en: Verifies a child frame tied to a project agent restores ancestor-message lineage across frames and passes the correct agent instance id into run context.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_WithProjectAgentChildFrame_RestoresAncestorLineageAcrossFrames()
    {
        var providerId = Guid.NewGuid();
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var providerResolverMock = new Mock<IProviderResolver>();
        providerResolverMock
            .Setup(item => item.ResolveAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResolvedProvider());

        var mcpToolServiceMock = new Mock<IAgentMcpToolService>();
        mcpToolServiceMock
            .Setup(item => item.LoadEnabledToolsAsync(It.IsAny<AgentMcpToolLoadContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var promptGeneratorMock = new Mock<IAgentPromptGenerator>();
        promptGeneratorMock
            .Setup(item => item.PromptBuildAsync(It.IsAny<AgentRole>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentRole role, AgentContext _, CancellationToken _) => $"prompt::{role.Name}");
        var fakeProvider = new CaptureAgentProvider();
        var agentFactory = new AgentFactory([fakeProvider]);

        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IAgentRoleRepository>(sp => new AgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectAgentRoleRepository>(sp => new ProjectAgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectRepository>(sp => new ProjectRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IChatMessageRepository>(sp => new ChatMessageRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProviderResolver>(_ => providerResolverMock.Object)
            .BuildServiceProvider();

        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var projectAgentId = Guid.NewGuid();
        var rootFrameId = Guid.NewGuid();
        var childFrameId = Guid.NewGuid();
        var rootUserMessageId = Guid.NewGuid();
        var rootAssistantMessageId = Guid.NewGuid();

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            var role = new AgentRole
            {
                Id = roleId,
                Name = "开发工程师",
                JobTitle = "producer",
                ProviderType = "fake",
                ModelProviderId = providerId,
                ModelName = "gpt-4o-mini",
                IsActive = true
            };

            var project = new Project
            {
                Id = projectId,
                Name = "Child Frame Restore",
                DefaultProviderId = providerId,
                Language = "zh-CN"
            };

            var projectAgent = new ProjectAgentRole
            {
                Id = projectAgentId,
                ProjectId = projectId,
                AgentRoleId = roleId,
                AgentRole = role,
                Project = project
            };

            db.AgentRoles.Add(role);
            db.Projects.Add(project);
            db.ProjectAgentRoles.Add(projectAgent);
            db.ChatSessions.Add(new ChatSession
            {
                Id = sessionId,
                ProjectId = projectId,
                InitialInput = "我要登录功能",
                Scene = SessionSceneTypes.ProjectGroup
            });
            db.ChatFrames.AddRange(
                new ChatFrame
                {
                    Id = rootFrameId,
                    SessionId = sessionId,
                    Depth = 0,
                    Purpose = "我要登录功能"
                },
                new ChatFrame
                {
                    Id = childFrameId,
                    SessionId = sessionId,
                    ParentFrameId = rootFrameId,
                    Depth = 1,
                    Purpose = "请实现登录接口"
                });
            db.ChatMessages.AddRange(
                new OpenStaff.Entities.ChatMessage
                {
                    Id = rootUserMessageId,
                    SessionId = sessionId,
                    FrameId = rootFrameId,
                    Role = MessageRoles.User,
                    Content = "我要登录功能",
                    SequenceNo = 0
                },
                new OpenStaff.Entities.ChatMessage
                {
                    Id = rootAssistantMessageId,
                    SessionId = sessionId,
                    FrameId = rootFrameId,
                    ParentMessageId = rootUserMessageId,
                    Role = MessageRoles.Assistant,
                    Content = "我来安排给 Producer",
                    SequenceNo = 1
                },
                new OpenStaff.Entities.ChatMessage
                {
                    SessionId = sessionId,
                    FrameId = childFrameId,
                    ParentMessageId = rootAssistantMessageId,
                    Role = MessageRoles.Assistant,
                    Content = "请实现登录接口",
                    ContentType = MessageContentTypes.Internal,
                    SequenceNo = 0
                });

            await db.SaveChangesAsync();
        }

        var factory = new ApplicationAgentRunFactory(
            services.GetRequiredService<IServiceScopeFactory>(),
            agentFactory,
            promptGeneratorMock.Object,
            mcpToolServiceMock.Object,
            new StubGitHubCopilotSessionManager(),
            services.GetRequiredService<ILogger<ApplicationAgentRunFactory>>());

        var prepared = await factory.PrepareAsync(
            new CreateMessageRequest(
                Scene: MessageScene.ProjectGroup,
                MessageContext: new MessageContext(
                    ProjectId: projectId,
                    SessionId: sessionId,
                    ParentMessageId: rootAssistantMessageId,
                    FrameId: childFrameId,
                    ParentFrameId: rootFrameId,
                    TaskId: null,
                    ProjectAgentRoleId: projectAgentId,
                    TargetRole: "producer",
                    InitiatorRole: BuiltinRoleTypes.Secretary,
                    Extra: null),
                InputRole: ChatRole.Assistant,
                Input: "请实现登录接口"),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal("producer", prepared.AgentRole);
        Assert.Equal(3, prepared.Messages.Count);
        Assert.Equal(ChatRole.User, prepared.Messages[0].Role);
        Assert.Equal("我要登录功能", prepared.Messages[0].Text);
        Assert.Equal(ChatRole.Assistant, prepared.Messages[1].Role);
        Assert.Equal("我来安排给 Producer", prepared.Messages[1].Text);
        Assert.Equal(ChatRole.Assistant, prepared.Messages[2].Role);
        Assert.Equal("请实现登录接口", prepared.Messages[2].Text);
        Assert.Equal(projectAgentId, fakeProvider.LastContext?.AgentInstanceId);
    }

    /// <summary>
    /// zh-CN: 验证目录角色查找对大小写不敏感，保证外部调用方使用不同写法时仍能命中正确角色。
    /// en: Verifies catalog role lookup is case-insensitive so external callers can target the correct role with varying casing.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_WithCatalogTargetRole_ResolvesCaseInsensitiveRoleMatch()
    {
        var providerId = Guid.NewGuid();
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var providerResolverMock = new Mock<IProviderResolver>();
        providerResolverMock
            .Setup(item => item.ResolveAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResolvedProvider());

        var mcpToolServiceMock = new Mock<IAgentMcpToolService>();
        mcpToolServiceMock
            .Setup(item => item.LoadEnabledToolsAsync(It.IsAny<AgentMcpToolLoadContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var promptGeneratorMock = new Mock<IAgentPromptGenerator>();
        promptGeneratorMock
            .Setup(item => item.PromptBuildAsync(It.IsAny<AgentRole>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentRole role, AgentContext _, CancellationToken _) => $"prompt::{role.Name}");

        var fakeProvider = new CaptureAgentProvider();
        var agentFactory = new AgentFactory([fakeProvider]);

        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IAgentRoleRepository>(sp => new AgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectAgentRoleRepository>(sp => new ProjectAgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectRepository>(sp => new ProjectRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IChatMessageRepository>(sp => new ChatMessageRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProviderResolver>(_ => providerResolverMock.Object)
            .BuildServiceProvider();

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.AgentRoles.Add(new AgentRole
            {
                Id = Guid.NewGuid(),
                Name = "秘书",
                JobTitle = "secretary",
                ProviderType = "fake",
                ModelProviderId = providerId,
                ModelName = "gpt-4o-mini",
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var factory = new ApplicationAgentRunFactory(
            services.GetRequiredService<IServiceScopeFactory>(),
            agentFactory,
            promptGeneratorMock.Object,
            mcpToolServiceMock.Object,
            new StubGitHubCopilotSessionManager(),
            services.GetRequiredService<ILogger<ApplicationAgentRunFactory>>());

        var prepared = await factory.PrepareAsync(
            new CreateMessageRequest(
                Scene: MessageScene.ProjectBrainstorm,
                MessageContext: new MessageContext(
                    ProjectId: null,
                    SessionId: null,
                    ParentMessageId: null,
                    FrameId: null,
                    ParentFrameId: null,
                    TaskId: null,
                    ProjectAgentRoleId: null,
                    TargetRole: "SeCrEtArY",
                    InitiatorRole: "user",
                    Extra: null),
                InputRole: ChatRole.User,
                Input: "帮我梳理需求"),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal("secretary", prepared.AgentRole);
        Assert.Single(prepared.Messages);
        Assert.Equal("帮我梳理需求", prepared.Messages[0].Text);
        Assert.Equal("秘书", fakeProvider.LastRole?.Name);
    }

    /// <summary>
    /// zh-CN: 验证当内置秘书只以“中文名称 + IsBuiltin”存在时，TargetRole=secretary 仍能正确解析。
    /// 这对应当前启动种子真实写库形态，避免脑暴续写时又在角色解析阶段失败。
    /// en: Verifies TargetRole=secretary still resolves when the builtin secretary row only has the Chinese display name plus IsBuiltin.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_WithBuiltinSecretaryDisplayNameOnly_StillResolvesSecretaryTargetRole()
    {
        var providerId = Guid.NewGuid();
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var providerResolverMock = new Mock<IProviderResolver>();
        providerResolverMock
            .Setup(item => item.ResolveAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResolvedProvider());

        var mcpToolServiceMock = new Mock<IAgentMcpToolService>();
        mcpToolServiceMock
            .Setup(item => item.LoadEnabledToolsAsync(It.IsAny<AgentMcpToolLoadContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var promptGeneratorMock = new Mock<IAgentPromptGenerator>();
        promptGeneratorMock
            .Setup(item => item.PromptBuildAsync(It.IsAny<AgentRole>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentRole role, AgentContext _, CancellationToken _) => $"prompt::{role.Name}");

        var fakeProvider = new CaptureAgentProvider();
        var agentFactory = new AgentFactory([fakeProvider]);

        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IAgentRoleRepository>(sp => new AgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectAgentRoleRepository>(sp => new ProjectAgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectRepository>(sp => new ProjectRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IChatMessageRepository>(sp => new ChatMessageRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProviderResolver>(_ => providerResolverMock.Object)
            .BuildServiceProvider();

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.AgentRoles.Add(new AgentRole
            {
                Id = Guid.NewGuid(),
                Name = "秘书",
                JobTitle = null,
                ProviderType = "fake",
                ModelProviderId = providerId,
                ModelName = "gpt-4o-mini",
                Source = AgentSource.Builtin,
                IsBuiltin = true,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var factory = new ApplicationAgentRunFactory(
            services.GetRequiredService<IServiceScopeFactory>(),
            agentFactory,
            promptGeneratorMock.Object,
            mcpToolServiceMock.Object,
            new StubGitHubCopilotSessionManager(),
            services.GetRequiredService<ILogger<ApplicationAgentRunFactory>>());

        var prepared = await factory.PrepareAsync(
            new CreateMessageRequest(
                Scene: MessageScene.ProjectBrainstorm,
                MessageContext: new MessageContext(
                    ProjectId: null,
                    SessionId: null,
                    ParentMessageId: null,
                    FrameId: null,
                    ParentFrameId: null,
                    TaskId: null,
                    ProjectAgentRoleId: null,
                    TargetRole: BuiltinRoleTypes.Secretary,
                    InitiatorRole: "user",
                    Extra: null),
                InputRole: ChatRole.User,
                Input: "继续梳理需求"),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal(BuiltinRoleTypes.Secretary, prepared.AgentRole);
        Assert.Equal("秘书", fakeProvider.LastRole?.Name);
    }

    /// <summary>
    /// zh-CN: 验证按 AgentRoleId 准备运行时，会应用覆盖 JSON 并基于覆盖后的角色重新构造提示词。
    /// en: Verifies the AgentRoleId path applies override JSON and rebuilds the prompt from the overridden role snapshot.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_WithAgentRoleIdAndOverride_AppliesOverrideAndBuildsPrompt()
    {
        var providerId = Guid.NewGuid();
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var providerResolverMock = new Mock<IProviderResolver>();
        providerResolverMock
            .Setup(item => item.ResolveAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResolvedProvider());

        AgentMcpToolLoadContext? capturedMcpContext = null;
        var mcpToolServiceMock = new Mock<IAgentMcpToolService>();
        mcpToolServiceMock
            .Setup(item => item.LoadEnabledToolsAsync(It.IsAny<AgentMcpToolLoadContext>(), It.IsAny<CancellationToken>()))
            .Callback((AgentMcpToolLoadContext context, CancellationToken _) => capturedMcpContext = context)
            .ReturnsAsync([]);

        var promptGeneratorMock = new Mock<IAgentPromptGenerator>();
        promptGeneratorMock
            .Setup(item => item.PromptBuildAsync(It.IsAny<AgentRole>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentRole role, AgentContext _, CancellationToken _) => $"prompt::{role.Name}");

        var fakeProvider = new CaptureAgentProvider();
        var agentFactory = new AgentFactory([fakeProvider]);

        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IAgentRoleRepository>(sp => new AgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectAgentRoleRepository>(sp => new ProjectAgentRoleRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProjectRepository>(sp => new ProjectRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IChatMessageRepository>(sp => new ChatMessageRepository(sp.GetRequiredService<AppDbContext>()))
            .AddScoped<IProviderResolver>(_ => providerResolverMock.Object)
            .BuildServiceProvider();

        var roleId = Guid.NewGuid();

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.AgentRoles.Add(new AgentRole
            {
                Id = roleId,
                Name = "原始角色",
                JobTitle = "architect",
                ProviderType = "fake",
                ModelProviderId = providerId,
                ModelName = "base-model",
                Source = AgentSource.Vendor,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var factory = new ApplicationAgentRunFactory(
            services.GetRequiredService<IServiceScopeFactory>(),
            agentFactory,
            promptGeneratorMock.Object,
            mcpToolServiceMock.Object,
            new StubGitHubCopilotSessionManager(),
            services.GetRequiredService<ILogger<ApplicationAgentRunFactory>>());

        var overrideJson = """
            {
              "name": "覆盖后的架构师",
              "modelName": "override-model",
              "temperature": 0.35,
              "soul": {
                "style": "严谨"
              }
            }
            """;

        var prepared = await factory.PrepareAsync(
            new CreateMessageRequest(
                Scene: MessageScene.Test,
                MessageContext: new MessageContext(
                    ProjectId: null,
                    SessionId: null,
                    ParentMessageId: null,
                    FrameId: null,
                    ParentFrameId: null,
                    TaskId: null,
                    ProjectAgentRoleId: null,
                    TargetRole: null,
                    InitiatorRole: null,
                    Extra: null),
                InputRole: ChatRole.User,
                Input: "帮我设计模块边界",
                AgentRoleId: roleId,
                OverrideJson: overrideJson),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal("architect", prepared.AgentRole);
        Assert.Equal("override-model", prepared.Model);
        Assert.Single(prepared.Messages);
        Assert.Equal("帮我设计模块边界", prepared.Messages[0].Text);
        Assert.NotNull(prepared.RunOptions);
        Assert.Equal("覆盖后的架构师", fakeProvider.LastRole?.Name);
        Assert.NotNull(capturedMcpContext);
        Assert.Equal(MessageScene.Test, capturedMcpContext!.Scene);
        Assert.Equal(roleId, capturedMcpContext.AgentRoleId);
        Assert.Null(capturedMcpContext.ProjectAgentRoleId);
    }

    /// <summary>
    /// zh-CN: 提供稳定的已解析 Provider 数据，让各测试专注于工厂选择逻辑而不是 Provider 解析细节。
    /// en: Supplies stable resolved-provider data so the tests can focus on factory selection logic instead of resolver internals.
    /// </summary>
    private static ResolvedProvider CreateResolvedProvider()
    {
        return new ResolvedProvider
        {
            Account = new ProviderAccount
            {
                Name = "Test Provider",
                ProtocolType = "openai",
                IsEnabled = true
            },
            ApiKey = "test-api-key"
        };
    }

    private sealed class StubGitHubCopilotSessionManager : IGitHubCopilotSessionManager
    {
        public Task<GitHubCopilotPreparedSession> PrepareSessionAsync(
            Microsoft.Agents.AI.GitHub.Copilot.GitHubCopilotAgent agent,
            AgentContext context,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("GitHub Copilot sessions are not exercised in ApplicationAgentRunFactoryTests.");
    }

    private sealed class CaptureAgentProvider : IAgentProvider
    {
        public string ProviderType => "fake";
        public string DisplayName => "Fake";
        public AgentRole? LastRole { get; private set; }
        public AgentContext? LastContext { get; private set; }

        /// <summary>
        /// zh-CN: 记录工厂最终解析出的角色与上下文，再返回一个轻量代理供断言使用。
        /// en: Captures the role and context resolved by the factory, then returns a lightweight agent for assertions.
        /// </summary>
        public Task<IStaffAgent> CreateAgentAsync(AgentRole role, AgentContext context)
        {
            LastRole = role;
            LastContext = context;

            var chatClient = new Mock<IChatClient>().Object;
            AIAgent agent = new ChatClientAgent(chatClient, name: role.Name, instructions: string.Empty);
            return Task.FromResult(agent.AsStaffAgent(new ServiceCollection().BuildServiceProvider()));
        }
    }
}
