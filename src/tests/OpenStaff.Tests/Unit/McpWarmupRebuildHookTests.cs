using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OpenStaff.ApiServices;
using OpenStaff.Application.Conversations.Services;
using OpenStaff.Application.McpServers.Services;
using OpenStaff.Application.Projects.Services;
using OpenStaff.Application.Skills.Services;
using OpenStaff.Dtos;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Infrastructure.Export;
using OpenStaff.Infrastructure.Security;
using OpenStaff.Mcp;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Services;
using OpenStaff.Mcp.Sources;
using OpenStaff.Options;
using OpenStaff.Repositories;

namespace OpenStaff.Tests.Unit;

public sealed class McpWarmupRebuildHookTests
{
    [Fact]
    public async Task UpdateServerAsync_RebuildsWarmCache_ForBoundRole()
    {
        using var harness = new WarmupHarness();
        await using var scope = harness.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var role = new AgentRole
        {
            Name = "Monica",
            JobTitle = "secretary",
            IsActive = true
        };
        var server = CreateBraveServer();
        db.AgentRoles.Add(role);
        db.McpServers.Add(server);
        await db.SaveChangesAsync();

        db.AgentRoleMcpBindings.Add(new AgentRoleMcpBinding
        {
            AgentRoleId = role.Id,
            McpServerId = server.Id,
            IsEnabled = true
        });
        await db.SaveChangesAsync();

        var service = harness.CreateMcpServerApiService(db);
        var globalPath = harness.GetGlobalConfigPath(server.Id);
        Assert.False(File.Exists(globalPath));

        var result = await service.UpdateServerAsync(server.Id, new UpdateMcpServerInput
        {
            Description = "updated"
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(File.Exists(globalPath));
    }

    [Fact]
    public async Task ReplaceAgentRoleBindingsAsync_RebuildsWarmCache_ForUpdatedRoleBindings()
    {
        using var harness = new WarmupHarness();
        await using var scope = harness.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var role = new AgentRole
        {
            Name = "Architect",
            JobTitle = "architect",
            IsActive = true
        };
        var server = CreateBraveServer();
        db.AgentRoles.Add(role);
        db.McpServers.Add(server);
        await db.SaveChangesAsync();

        var service = harness.CreateMcpServerApiService(db);
        var globalPath = harness.GetGlobalConfigPath(server.Id);
        Assert.False(File.Exists(globalPath));

        await service.ReplaceAgentRoleBindingsAsync(new ReplaceAgentRoleMcpBindingsRequest
        {
            AgentRoleId = role.Id,
            Bindings =
            [
                new AgentRoleMcpBindingInput
                {
                    McpServerId = server.Id,
                    IsEnabled = true
                }
            ]
        }, CancellationToken.None);

        Assert.True(File.Exists(globalPath));
        Assert.Single(await db.AgentRoleMcpBindings.Where(item => item.AgentRoleId == role.Id).ToListAsync());
    }

    [Fact]
    public async Task SetProjectAgentsAsync_RebuildsProjectWarmCache_ForKnownProjectRegistrations()
    {
        using var harness = new WarmupHarness();
        await using var scope = harness.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secretaryRole = new AgentRole
        {
            Name = "Monica",
            JobTitle = "secretary",
            IsBuiltin = true,
            IsActive = true
        };
        var server = CreateBraveServer();
        var projectWorkspace = Path.Combine(harness.WorkspaceRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectWorkspace);
        var project = new Project
        {
            Name = "Warm Project",
            Status = ProjectStatus.Active,
            Phase = ProjectPhases.Brainstorming,
            Language = "zh-CN",
            WorkspacePath = projectWorkspace
        };

        db.AgentRoles.Add(secretaryRole);
        db.McpServers.Add(server);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        db.AgentRoleMcpBindings.Add(new AgentRoleMcpBinding
        {
            AgentRoleId = secretaryRole.Id,
            McpServerId = server.Id,
            IsEnabled = true
        });
        await db.SaveChangesAsync();

        AddProjectWarmRegistration(harness.WarmupCoordinator, project.Id, secretaryRole.Id, server.Id);

        var managedSkillStore = new Mock<IManagedSkillStore>();
        managedSkillStore
            .Setup(store => store.GetInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ManagedInstalledSkill>());

        var roleCapabilityBindingService = new RoleCapabilityBindingService(
            new AgentRoleRepository(db),
            new ProjectAgentRoleRepository(db),
            new McpServerRepository(db),
            new AgentRoleMcpBindingRepository(db),
            new AgentRoleSkillBindingRepository(db),
            new ProjectAgentRoleSkillBindingRepository(db),
            managedSkillStore.Object,
            db,
            NullLogger<RoleCapabilityBindingService>.Instance);
        var conversationTriggerService = new ConversationTriggerService(
            new ChatSessionRepository(db),
            new ChatFrameRepository(db),
            new ChatMessageRepository(db),
            new ProjectAgentRoleRepository(db),
            new AgentRoleRepository(db),
            db,
            NullLogger<ConversationTriggerService>.Instance);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workspaces:RootPath"] = harness.WorkspaceRoot
            })
            .Build();

        var service = new ProjectService(
            new ProjectRepository(db),
            new ProjectAgentRoleRepository(db),
            new AgentRoleRepository(db),
            new TaskItemRepository(db),
            new TaskDependencyRepository(db),
            new AgentEventRepository(db),
            new CheckpointRepository(db),
            new ChatSessionRepository(db),
            new ChatFrameRepository(db),
            new ChatMessageRepository(db),
            new SessionEventRepository(db),
            db,
            new ProjectExporter(new ProjectRepository(db), NullLogger<ProjectExporter>.Instance),
            new ProjectImporter(new ProjectRepository(db), db, NullLogger<ProjectImporter>.Instance),
            config,
            conversationTriggerService,
            roleCapabilityBindingService,
            NullLogger<ProjectService>.Instance,
            Microsoft.Extensions.Options.Options.Create(new OpenStaffOptions { WorkingDirectory = harness.WorkspaceRoot }),
            sessionRunner: null,
            mcpHub: harness.Hub,
            mcpWarmupCoordinator: harness.WarmupCoordinator);

        var globalPath = harness.GetGlobalConfigPath(server.Id);
        Assert.False(File.Exists(globalPath));

        await service.SetProjectAgentsAsync(project.Id, [], CancellationToken.None);

        Assert.True(File.Exists(globalPath));
    }

    [Fact]
    public async Task DeleteAsync_ForgetsProjectWarmRegistrations()
    {
        using var harness = new WarmupHarness();
        await using var scope = harness.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var projectWorkspace = Path.Combine(harness.WorkspaceRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectWorkspace);
        var project = new Project
        {
            Name = "Warm Delete",
            Status = ProjectStatus.Active,
            Phase = ProjectPhases.Brainstorming,
            Language = "zh-CN",
            WorkspacePath = projectWorkspace
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        AddProjectWarmRegistration(harness.WarmupCoordinator, project.Id, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(1, GetProjectWarmRegistrationCount(harness.WarmupCoordinator));

        var managedSkillStore = new Mock<IManagedSkillStore>();
        managedSkillStore
            .Setup(store => store.GetInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ManagedInstalledSkill>());
        var roleCapabilityBindingService = new RoleCapabilityBindingService(
            new AgentRoleRepository(db),
            new ProjectAgentRoleRepository(db),
            new McpServerRepository(db),
            new AgentRoleMcpBindingRepository(db),
            new AgentRoleSkillBindingRepository(db),
            new ProjectAgentRoleSkillBindingRepository(db),
            managedSkillStore.Object,
            db,
            NullLogger<RoleCapabilityBindingService>.Instance);
        var conversationTriggerService = new ConversationTriggerService(
            new ChatSessionRepository(db),
            new ChatFrameRepository(db),
            new ChatMessageRepository(db),
            new ProjectAgentRoleRepository(db),
            new AgentRoleRepository(db),
            db,
            NullLogger<ConversationTriggerService>.Instance);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workspaces:RootPath"] = harness.WorkspaceRoot
            })
            .Build();
        var service = new ProjectService(
            new ProjectRepository(db),
            new ProjectAgentRoleRepository(db),
            new AgentRoleRepository(db),
            new TaskItemRepository(db),
            new TaskDependencyRepository(db),
            new AgentEventRepository(db),
            new CheckpointRepository(db),
            new ChatSessionRepository(db),
            new ChatFrameRepository(db),
            new ChatMessageRepository(db),
            new SessionEventRepository(db),
            db,
            new ProjectExporter(new ProjectRepository(db), NullLogger<ProjectExporter>.Instance),
            new ProjectImporter(new ProjectRepository(db), db, NullLogger<ProjectImporter>.Instance),
            config,
            conversationTriggerService,
            roleCapabilityBindingService,
            NullLogger<ProjectService>.Instance,
            Microsoft.Extensions.Options.Options.Create(new OpenStaffOptions { WorkingDirectory = harness.WorkspaceRoot }),
            sessionRunner: null,
            mcpHub: harness.Hub,
            mcpWarmupCoordinator: harness.WarmupCoordinator);

        var deleted = await service.DeleteAsync(project.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Equal(0, GetProjectWarmRegistrationCount(harness.WarmupCoordinator));
    }

    private static McpServer CreateBraveServer()
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Brave Search",
            IsEnabled = true,
            TransportType = McpTransportTypes.Stdio,
            Mode = McpServerModes.Local,
            Source = McpSources.Custom,
            NpmPackage = "@modelcontextprotocol/server-brave-search",
            DefaultConfig =
                """
                {
                  "schema": "openstaff.mcp-template.v1",
                  "default_profile_id": "package-npm",
                  "profiles": [
                    {
                      "id": "package-npm",
                      "profile_type": "package",
                      "transport_type": "stdio",
                      "command": "npx",
                      "args_template": ["-y", "@modelcontextprotocol/server-brave-search"],
                      "env_template": {
                        "BRAVE_API_KEY": "${param:apiKey}"
                      }
                    }
                  ],
                  "parameter_schema": [
                    {
                      "key": "apiKey",
                      "type": "password",
                      "required": true,
                      "default_value": ""
                    }
                  ]
                }
                """
        };

    private static void AddProjectWarmRegistration(McpWarmupCoordinator coordinator, Guid projectId, Guid agentRoleId, Guid serverId)
    {
        var registrationType = typeof(McpWarmupCoordinator).GetNestedType("ProjectWarmRegistration", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ProjectWarmRegistration type not found.");
        var ctor = registrationType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single(candidate =>
            {
                var parameters = candidate.GetParameters();
                return parameters.Length == 4
                    && parameters[0].ParameterType == typeof(string)
                    && parameters[1].ParameterType == typeof(Guid)
                    && parameters[2].ParameterType == typeof(Guid)
                    && parameters[3].ParameterType == typeof(Guid);
            });
        var cacheKey = $"project:{projectId:N}:role:{agentRoleId:N}:server:{serverId:N}";
        var registration = ctor.Invoke([cacheKey, projectId, agentRoleId, serverId]);
        var registrations = GetProjectRegistrationDictionary(coordinator);
        registrations.GetType().GetProperty("Item")!.SetValue(registrations, registration, [cacheKey]);
    }

    private static int GetProjectWarmRegistrationCount(McpWarmupCoordinator coordinator)
    {
        var registrations = GetProjectRegistrationDictionary(coordinator);
        return (int)(registrations.GetType().GetProperty("Count")!.GetValue(registrations) ?? 0);
    }

    private static object GetProjectRegistrationDictionary(McpWarmupCoordinator coordinator)
        => typeof(McpWarmupCoordinator).GetField("_projectRegistrations", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(coordinator)!
            ?? throw new InvalidOperationException("Warm registration cache not found.");

    private sealed class WarmupHarness : IDisposable
    {
        private readonly SqliteConnection _connection;

        public WarmupHarness()
        {
            WorkingDirectory = Path.Combine(Path.GetTempPath(), "openstaff-mcp-rebuild-tests", Guid.NewGuid().ToString("N"));
            WorkspaceRoot = Path.Combine(Path.GetTempPath(), "openstaff-mcp-rebuild-workspaces", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(WorkingDirectory);
            Directory.CreateDirectory(WorkspaceRoot);

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var services = new ServiceCollection()
                .AddLogging()
                .AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>());
            services.AddScoped<IMcpServerRepository, McpServerRepository>();
            services.AddScoped<IAgentRoleRepository, AgentRoleRepository>();
            services.AddScoped<IAgentRoleMcpBindingRepository, AgentRoleMcpBindingRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddScoped<IProjectAgentRoleRepository, ProjectAgentRoleRepository>();
            services.AddScoped<IAgentRoleSkillBindingRepository, AgentRoleSkillBindingRepository>();
            services.AddScoped<IProjectAgentRoleSkillBindingRepository, ProjectAgentRoleSkillBindingRepository>();

            services.AddSingleton(new EncryptionService("mcp-rebuild-hook-tests"));
            services.AddSingleton(new McpRuntimeParameterDefaultsService());
            services.AddSingleton(new McpStructuredMetadataFactory());
            services.AddSingleton<McpProfileConnectionRenderer>();
            services.AddSingleton<IMcpConfigurationFileStore>(sp => new McpConfigurationFileStore(
                Microsoft.Extensions.Options.Options.Create(new OpenStaffOptions { WorkingDirectory = WorkingDirectory }),
                sp.GetRequiredService<EncryptionService>(),
                sp.GetRequiredService<McpRuntimeParameterDefaultsService>(),
                sp.GetRequiredService<McpProfileConnectionRenderer>()));
            services.AddSingleton<McpResolvedConnectionFactory>();
            services.AddSingleton<IOptions<OpenStaffMcpOptions>>(Microsoft.Extensions.Options.Options.Create(new OpenStaffMcpOptions
            {
                EnableStartupWarmup = true,
                PinProjectClientsAfterFirstUse = true,
                LazyClientIdleTimeoutSeconds = 300
            }));
            services.AddSingleton<McpHub>();
            services.AddSingleton<McpWarmupCoordinator>();

            Services = services.BuildServiceProvider();

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            Hub = Services.GetRequiredService<McpHub>();
            WarmupCoordinator = Services.GetRequiredService<McpWarmupCoordinator>();
            ConfigurationFileStore = Services.GetRequiredService<IMcpConfigurationFileStore>();
            ResolvedConnectionFactory = Services.GetRequiredService<McpResolvedConnectionFactory>();
            RuntimeParameterDefaults = Services.GetRequiredService<McpRuntimeParameterDefaultsService>();
            StructuredMetadataFactory = Services.GetRequiredService<McpStructuredMetadataFactory>();
            ProfileConnectionRenderer = Services.GetRequiredService<McpProfileConnectionRenderer>();
        }

        public string WorkingDirectory { get; }
        public string WorkspaceRoot { get; }
        public ServiceProvider Services { get; }
        public McpHub Hub { get; }
        public McpWarmupCoordinator WarmupCoordinator { get; }
        public IMcpConfigurationFileStore ConfigurationFileStore { get; }
        public McpResolvedConnectionFactory ResolvedConnectionFactory { get; }
        public McpRuntimeParameterDefaultsService RuntimeParameterDefaults { get; }
        public McpStructuredMetadataFactory StructuredMetadataFactory { get; }
        public McpProfileConnectionRenderer ProfileConnectionRenderer { get; }

        public string GetGlobalConfigPath(Guid serverId)
            => Path.Combine(WorkingDirectory, ".mcp", "global", $"{serverId:N}.json");

        public McpServerApiService CreateMcpServerApiService(AppDbContext db)
        {
            var installedMcpService = new Mock<IInstalledMcpService>();
            installedMcpService
                .Setup(service => service.ListInstalledAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<InstalledMcp>());
            return new McpServerApiService(
                new McpServerRepository(db),
                new AgentRoleMcpBindingRepository(db),
                new AgentRoleRepository(db),
                new ProjectAgentRoleRepository(db),
                db,
                ConfigurationFileStore,
                ResolvedConnectionFactory,
                Hub,
                RuntimeParameterDefaults,
                StructuredMetadataFactory,
                ProfileConnectionRenderer,
                Array.Empty<IMcpCatalogSource>(),
                Mock.Of<IMcpCatalogService>(),
                Mock.Of<IMcpInstallationService>(),
                installedMcpService.Object,
                Mock.Of<IMcpRuntimeResolver>(),
                Mock.Of<IMcpUninstallService>(),
                Mock.Of<IMcpRepairService>(),
                WarmupCoordinator);
        }

        public void Dispose()
        {
            Hub.Dispose();
            Services.Dispose();
            _connection.Dispose();

            if (Directory.Exists(WorkingDirectory))
                Directory.Delete(WorkingDirectory, recursive: true);
            if (Directory.Exists(WorkspaceRoot))
                Directory.Delete(WorkspaceRoot, recursive: true);
        }
    }
}
