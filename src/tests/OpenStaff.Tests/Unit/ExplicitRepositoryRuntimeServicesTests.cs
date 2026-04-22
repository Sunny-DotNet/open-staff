using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OpenHub.Agents;
using OpenStaff.Agent;
using OpenStaff.Agent.Builtin;
using OpenStaff.Agent.Services;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Application.McpServers.Services;
using OpenStaff.Application.Orchestration.Services;
using OpenStaff.Application.Projects.Services;
using OpenStaff.Application.Seeding.Services;
using OpenStaff.Application.Skills.Services;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Infrastructure.Security;
using OpenStaff.Provider;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;
using OpenStaff.Repositories;
using OpenStaff.Mcp;
using OpenStaff.Mcp.Persistence;

namespace OpenStaff.Tests.Unit;

public class ExplicitRepositoryRuntimeServicesTests
{
    [Fact]
    public async Task McpHardResetService_StartAsync_RemovesLegacyMcpState_Once()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), $"openstaff-mcp-reset-{Guid.NewGuid():N}");
        using var context = new RepositoryScopeTestContext(services =>
        {
            services.AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>());
            services.AddScoped<IMcpServerRepository, McpServerRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddScoped<IAgentRoleMcpBindingRepository, AgentRoleMcpBindingRepository>();
            services.AddSingleton<IMcpDataDirectoryLayout>(_ =>
                new McpDataDirectoryLayout(Microsoft.Extensions.Options.Options.Create(new OpenStaffMcpOptions
                {
                    DataRootPath = dataRoot
                })));
        });

        Directory.CreateDirectory(dataRoot);
        await File.WriteAllTextAsync(Path.Combine(dataRoot, "legacy.txt"), "legacy");

        var role = new AgentRole { Name = "Legacy Role", IsActive = true };
        var project = new Project { Name = "Legacy Project", WorkspacePath = @"A:\legacy-project" };
        var server = new McpServer { Name = "Legacy MCP" };
        context.Db.AgentRoles.Add(role);
        context.Db.Projects.Add(project);
        context.Db.McpServers.Add(server);
        await context.Db.SaveChangesAsync();

        Directory.CreateDirectory(Path.Combine(project.WorkspacePath!, ".mcp"));
        await File.WriteAllTextAsync(Path.Combine(project.WorkspacePath!, ".mcp", $"{server.Id}.json"), "{}");
        context.Db.AgentRoleMcpBindings.Add(new AgentRoleMcpBinding { AgentRoleId = role.Id, McpServerId = server.Id });
        await context.Db.SaveChangesAsync();

        var service = new McpHardResetService(
            context.Services.GetRequiredService<IServiceScopeFactory>(),
            context.Services.GetRequiredService<IMcpDataDirectoryLayout>(),
            NullLogger<McpHardResetService>.Instance);

        await service.StartAsync(CancellationToken.None);
        Assert.Empty(context.Db.McpServers);
        Assert.Empty(context.Db.AgentRoleMcpBindings);
        Assert.True(File.Exists(Path.Combine(dataRoot, ".mcp-hard-reset-v3")));
        Assert.DoesNotContain(Path.Combine(dataRoot, "legacy.txt"), Directory.EnumerateFileSystemEntries(dataRoot));
        Assert.False(Directory.Exists(Path.Combine(project.WorkspacePath!, ".mcp")));

        context.Db.McpServers.Add(new McpServer { Name = "Fresh MCP" });
        await context.Db.SaveChangesAsync();

        await service.StartAsync(CancellationToken.None);
        Assert.Single(context.Db.McpServers);
    }

    [Fact]
    public async Task RoleCapabilityBindingService_SeedsRecommendedBindings_ForMatchedRoles()
    {
        var managedSkillStore = new Mock<IManagedSkillStore>();
        managedSkillStore
            .Setup(store => store.GetInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateInstalledSkill("github", "awesome-copilot", "gh-cli"),
                CreateInstalledSkill("anthropics", "skills", "frontend-design")
            ]);

        using var context = new RepositoryScopeTestContext(services =>
        {
            services.AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>());
            services.AddScoped<IAgentRoleRepository, AgentRoleRepository>();
            services.AddScoped<IProjectAgentRoleRepository, ProjectAgentRoleRepository>();
            services.AddScoped<IMcpServerRepository, McpServerRepository>();
            services.AddScoped<IAgentRoleMcpBindingRepository, AgentRoleMcpBindingRepository>();
            services.AddScoped<IAgentRoleSkillBindingRepository, AgentRoleSkillBindingRepository>();
            services.AddScoped<IProjectAgentRoleSkillBindingRepository, ProjectAgentRoleSkillBindingRepository>();
            services.AddScoped<RoleCapabilityBindingService>();
            services.AddSingleton(managedSkillStore.Object);
        });

        var monica = new AgentRole { Name = "Monica", JobTitle = "秘书", IsActive = true, IsBuiltin = true };
        var feifei = new AgentRole { Name = "菲菲", JobTitle = "美工", IsActive = true };
        context.Db.AgentRoles.AddRange(monica, feifei);
        context.Db.McpServers.AddRange(
            new McpServer { Name = "Filesystem", IsEnabled = true, NpmPackage = "@modelcontextprotocol/server-filesystem" },
            new McpServer { Name = "Everything", IsEnabled = true },
            new McpServer { Name = "Fetch", IsEnabled = true },
            new McpServer { Name = "Brave Search", IsEnabled = true },
            new McpServer { Name = "GitHub", IsEnabled = true },
            new McpServer { Name = "Memory", IsEnabled = true },
            new McpServer { Name = "Sequential Thinking", IsEnabled = true },
            new McpServer { Name = "Puppeteer", IsEnabled = true });
        await context.Db.SaveChangesAsync();

        await using var scope = context.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<RoleCapabilityBindingService>();

        var firstPass = await service.SeedDefaultRoleBindingsAsync(CancellationToken.None);
        var secondPass = await service.SeedDefaultRoleBindingsAsync(CancellationToken.None);

        Assert.True(firstPass > 0);
        Assert.Equal(0, secondPass);

        var monicaMcpNames = await context.Db.AgentRoleMcpBindings
            .Where(binding => binding.AgentRoleId == monica.Id)
            .Include(binding => binding.McpServer)
            .Select(binding => binding.McpServer!.Name)
            .ToListAsync();
        var monicaSkillKeys = await context.Db.AgentRoleSkillBindings
            .Where(binding => binding.AgentRoleId == monica.Id)
            .Select(binding => binding.SkillInstallKey)
            .ToListAsync();
        var feifeiMcpNames = await context.Db.AgentRoleMcpBindings
            .Where(binding => binding.AgentRoleId == feifei.Id)
            .Include(binding => binding.McpServer)
            .Select(binding => binding.McpServer!.Name)
            .ToListAsync();
        var feifeiSkillKeys = await context.Db.AgentRoleSkillBindings
            .Where(binding => binding.AgentRoleId == feifei.Id)
            .Select(binding => binding.SkillInstallKey)
            .ToListAsync();

        Assert.Contains("GitHub", monicaMcpNames);
        Assert.Contains("Memory", monicaMcpNames);
        Assert.Contains("github/awesome-copilot:gh-cli", monicaSkillKeys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Puppeteer", feifeiMcpNames);
        Assert.Contains("anthropics/skills:frontend-design", feifeiSkillKeys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentSkillRuntimeService_LoadRuntimePayloadAsync_UsesExplicitSkillRepositories()
    {
        using var context = new RepositoryScopeTestContext(services =>
        {
            services.AddScoped<IAgentRoleSkillBindingRepository, AgentRoleSkillBindingRepository>();
            services.AddScoped<IProjectAgentRoleSkillBindingRepository, ProjectAgentRoleSkillBindingRepository>();
        });

        var roleId = Guid.NewGuid();
        context.Db.AgentRoles.Add(new AgentRole
        {
            Id = roleId,
            Name = "Skill Role",
            JobTitle = "skill-role",
            IsActive = true
        });
        context.Db.AgentRoleSkillBindings.Add(new AgentRoleSkillBinding
        {
            AgentRoleId = roleId,
            SkillInstallKey = "owner.repo.skill",
            SkillId = "skill",
            Name = "Skill",
            DisplayName = "Skill Display",
            Source = SkillSourceKeys.SkillsSh,
            Owner = "owner",
            Repo = "repo",
            IsEnabled = true
        });
        await context.Db.SaveChangesAsync();

        var installedSkill = new ManagedInstalledSkill(
            Guid.NewGuid(),
            "owner.repo.skill",
            SkillSourceKeys.SkillsSh,
            "owner/repo",
            "owner",
            "repo",
            "skill",
            "Skill",
            "Skill Display",
            "https://github.com/owner/repo",
            1,
            @"A:\skills\owner.repo.skill",
            SkillInstallStatuses.Installed,
            null,
            "sha",
            DateTime.UtcNow,
            DateTime.UtcNow);
        var managedSkillStore = new Mock<IManagedSkillStore>();
        managedSkillStore
            .Setup(store => store.GetInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([installedSkill]);

        var service = new AgentSkillRuntimeService(
            context.Services.GetRequiredService<IServiceScopeFactory>(),
            managedSkillStore.Object,
            NullLogger<AgentSkillRuntimeService>.Instance);

        var payload = await service.LoadRuntimePayloadAsync(
            new AgentSkillLoadContext(MessageScene.Test, null, roleId),
            CancellationToken.None);

        var skill = Assert.Single(Assert.IsType<AgentSkillRuntimePayload>(payload).Skills);
        Assert.Equal(installedSkill.InstallKey, skill.InstallKey);
        Assert.Empty(payload!.MissingBindings);
    }

    [Fact]
    public async Task AgentMcpToolService_LoadEnabledToolsAsync_UsesExplicitRoleBindings()
    {
        using var context = new RepositoryScopeTestContext(services =>
        {
            services.AddScoped<IAgentRoleMcpBindingRepository, AgentRoleMcpBindingRepository>();
            services.AddScoped<IProjectAgentRoleRepository, ProjectAgentRoleRepository>();
        });
        using var manager = new McpHub(NullLoggerFactory.Instance);
        var service = new AgentMcpToolService(
            context.Services.GetRequiredService<IServiceScopeFactory>(),
            CreateMcpConfigurationFileStore(),
            CreateResolvedConnectionFactory(),
            manager,
            NullLogger<AgentMcpToolService>.Instance);

        var tools = await service.LoadEnabledToolsAsync(
            new AgentMcpToolLoadContext(MessageScene.Test, null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task AgentMcpToolService_EnsureToolsAllowedAsync_UsesExplicitProjectBindingsAndContext()
    {
        using var context = new RepositoryScopeTestContext(services =>
        {
            services.AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>());
            services.AddScoped<IAgentRoleMcpBindingRepository, AgentRoleMcpBindingRepository>();
            services.AddScoped<IProjectAgentRoleRepository, ProjectAgentRoleRepository>();
        });
        using var manager = new McpHub(NullLoggerFactory.Instance);
        var service = new AgentMcpToolService(
            context.Services.GetRequiredService<IServiceScopeFactory>(),
            CreateMcpConfigurationFileStore(),
            CreateResolvedConnectionFactory(),
            manager,
            NullLogger<AgentMcpToolService>.Instance);

        var result = await service.EnsureToolsAllowedAsync(
            Guid.NewGuid(),
            ["tool-a"],
            CancellationToken.None);

        Assert.Empty(result.SatisfiedTools);
        Assert.Equal(["tool-a"], result.MissingTools);
        Assert.False(result.Changed);
    }

    private static ManagedInstalledSkill CreateInstalledSkill(string owner, string repo, string skillId)
        => new(
            Guid.NewGuid(),
            $"{owner}/{repo}:{skillId}",
            SkillSourceKeys.SkillsSh,
            $"{owner}/{repo}",
            owner,
            repo,
            skillId,
            skillId,
            skillId,
            $"https://github.com/{owner}/{repo}",
            1,
            $@"A:\skills\{owner}-{repo}-{skillId}",
            SkillInstallStatuses.Installed,
            null,
            "sha",
            DateTime.UtcNow,
            DateTime.UtcNow);

    private static BuiltinAgentProvider CreateBuiltinProvider()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IProviderAccountRepository>(_ =>
            {
                var repository = new Mock<IProviderAccountRepository>();
                repository
                    .Setup(item => item.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .Returns((Guid id, CancellationToken _) => ValueTask.FromResult<ProviderAccount?>(new ProviderAccount { Id = id, ProtocolType = "openai", Name = "Test" }));
                return repository.Object;
            })
            .AddSingleton<ICurrentProviderDetail>(new Mock<ICurrentProviderDetail>().Object)
            .BuildServiceProvider();
        var protocolFactory = new Mock<IProtocolFactory>();
        protocolFactory
            .Setup(factory => factory.CreateProtocolWithEnv(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new StubProtocol());
        var chatClientFactory = new ChatClientFactory(
            services.GetRequiredService<ILoggerFactory>(),
            protocolFactory.Object,
            new PlatformRegistry([new OpenAIChatClientFactoryPlatform()]),
            services);
        return new BuiltinAgentProvider(
            services,
            chatClientFactory,
            new Mock<IAgentPromptGenerator>().Object,
            services.GetRequiredService<ILoggerFactory>());
    }

    private static IMcpConfigurationFileStore CreateMcpConfigurationFileStore()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "openstaff-explicit-repo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        return new McpConfigurationFileStore(
            Microsoft.Extensions.Options.Options.Create(new OpenStaff.Options.OpenStaffOptions
            {
                WorkingDirectory = workingDirectory
            }),
            new EncryptionService("explicit-repository-runtime-tests"),
            new McpRuntimeParameterDefaultsService(),
            new McpProfileConnectionRenderer(new McpStructuredMetadataFactory()));
    }

    private static McpResolvedConnectionFactory CreateResolvedConnectionFactory()
        => new(new McpProfileConnectionRenderer(new McpStructuredMetadataFactory()), new McpRuntimeParameterDefaultsService());

    private sealed class RepositoryScopeTestContext : IDisposable
    {
        private readonly SqliteConnection _connection;

        public RepositoryScopeTestContext(Action<IServiceCollection> configureServices)
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var services = new ServiceCollection()
                .AddLogging()
                .AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            configureServices(services);

            Services = services.BuildServiceProvider();
            Db = Services.GetRequiredService<AppDbContext>();
            Db.Database.EnsureCreated();
        }

        public ServiceProvider Services { get; }
        public AppDbContext Db { get; }

        public void Dispose()
        {
            Services.Dispose();
            _connection.Dispose();
        }
    }

    private sealed class StubProtocol : IProtocol
    {
        public bool IsVendor => false;
        public string ProtocolKey => "openai";
        public string ProtocolName => "OpenAI";
        public string Logo => "OpenAI";

        public Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<ModelInfo>>([]);
    }

    private sealed class OpenAIChatClientFactoryPlatform : IPlatform, IHasChatClientFactory, IHasProtocol
    {
        public string PlatformKey => "openai";

        public IProtocol GetProtocol() => new StubProtocol();

        public IChatClientFactory GetChatClientFactory()
            => new StubOpenAIChatClientFactory();
    }

    private sealed class StubOpenAIChatClientFactory : IChatClientFactory
    {
        public Task<IChatClient> CreateAsync(
            ChatClientCreateRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new Mock<IChatClient>().Object);
    }
}

