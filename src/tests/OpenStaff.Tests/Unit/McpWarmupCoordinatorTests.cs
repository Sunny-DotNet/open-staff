using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenStaff.Application.McpServers.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Infrastructure.Security;
using OpenStaff.Mcp;
using OpenStaff.Options;
using OpenStaff.Repositories;

namespace OpenStaff.Tests.Unit;

public sealed class McpWarmupCoordinatorTests
{
    [Fact]
    public async Task WarmStartupConnectionsAsync_CreatesGlobalConfig_AndSkipsBraveClientWithoutApiKey()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "openstaff-mcp-warmup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        using var context = new RepositoryScopeTestContext(services =>
        {
            services.AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>());
            services.AddScoped<IAgentRoleMcpBindingRepository, AgentRoleMcpBindingRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddSingleton(new EncryptionService("mcp-warmup-tests"));
            services.AddSingleton(new McpRuntimeParameterDefaultsService());
            services.AddSingleton(new McpStructuredMetadataFactory());
            services.AddSingleton<McpProfileConnectionRenderer>();
            services.AddSingleton<IMcpConfigurationFileStore>(sp => new McpConfigurationFileStore(
                Microsoft.Extensions.Options.Options.Create(new OpenStaffOptions { WorkingDirectory = workingDirectory }),
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
        });

        var role = new AgentRole
        {
            Name = "Monica",
            JobTitle = "Coordinator",
            IsActive = true
        };
        var server = new McpServer
        {
            Id = Guid.NewGuid(),
            Name = "Brave Search",
            IsEnabled = true,
            TransportType = McpTransportTypes.Stdio,
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
        context.Db.AgentRoles.Add(role);
        context.Db.McpServers.Add(server);
        await context.Db.SaveChangesAsync();

        context.Db.AgentRoleMcpBindings.Add(new AgentRoleMcpBinding
        {
            AgentRoleId = role.Id,
            McpServerId = server.Id,
            IsEnabled = true
        });
        await context.Db.SaveChangesAsync();

        var coordinator = context.Services.GetRequiredService<McpWarmupCoordinator>();
        var manager = context.Services.GetRequiredService<McpHub>();

        await coordinator.WarmStartupConnectionsAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(workingDirectory, ".mcp", "global", $"{server.Id:N}.json")));
        Assert.Equal(0, GetPrivateDictionaryCount(manager, "_clients"));
        Assert.Equal(0, GetPrivateDictionaryCount(manager, "_toolSnapshots"));
    }

    private static int GetPrivateDictionaryCount(object owner, string fieldName)
    {
        var field = owner.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found.");
        var value = field.GetValue(owner)
            ?? throw new InvalidOperationException($"Field '{fieldName}' is null.");
        return (int)(value.GetType().GetProperty("Count")?.GetValue(value) ?? 0);
    }

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
}
