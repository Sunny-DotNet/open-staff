using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenStaff.Application.AgentRoles.Services;
using OpenStaff.Application.Seeding.Services;
using OpenStaff.Application.Skills.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Repositories;
using OpenStaff.Skills.Models;

namespace OpenStaff.Tests.Unit;

public class MonicaRoleSeedServiceTests
{
    private static readonly Guid MonicaRoleId = new("AC2E24E3-69D9-4112-9FF5-F59A2877A624");

    [Fact]
    public async Task StartAsync_ShouldSeedMonicaRoleFromEmbeddedTemplate()
    {
        await using var testContext = await CreateTestContextAsync();

        var service = new MonicaRoleSeedService(
            testContext.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MonicaRoleSeedService>.Instance);

        await service.StartAsync(CancellationToken.None);

        await using var verifyScope = testContext.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.AgentRoles.SingleAsync(item => item.Id == MonicaRoleId);

        Assert.Equal("Monica", role.Name);
        Assert.Equal("secretary", role.JobTitle);
        Assert.Equal("glm-5.1", role.ModelName);
        Assert.False(role.IsBuiltin);
        Assert.Equal(AgentSource.Custom, role.Source);
    }

    [Fact]
    public async Task StartAsync_ShouldLeaveExistingMonicaRoleUntouched()
    {
        await using var testContext = await CreateTestContextAsync();

        await using (var seedScope = testContext.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AgentRoles.Add(new AgentRole
            {
                Id = MonicaRoleId,
                Name = "Monica",
                JobTitle = "旧职位",
                Description = "旧描述",
                ModelName = "old-model",
                Source = AgentSource.Custom,
                IsBuiltin = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
            });
            await db.SaveChangesAsync();
        }

        var service = new MonicaRoleSeedService(
            testContext.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MonicaRoleSeedService>.Instance);

        await service.StartAsync(CancellationToken.None);

        await using var verifyScope = testContext.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await verifyDb.AgentRoles.SingleAsync(item => item.Id == MonicaRoleId);

        Assert.Equal("旧职位", role.JobTitle);
        Assert.Equal("old-model", role.ModelName);
        Assert.Equal("旧描述", role.Description);
    }

    private static async Task<TestContext> CreateTestContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IAgentRoleRepository, AgentRoleRepository>();
        services.AddScoped<IMcpServerRepository, McpServerRepository>();
        services.AddScoped<IAgentRoleMcpBindingRepository, AgentRoleMcpBindingRepository>();
        services.AddScoped<IAgentRoleSkillBindingRepository, AgentRoleSkillBindingRepository>();
        services.AddSingleton<IManagedSkillStore>(new FakeManagedSkillStore([]));
        services.AddScoped<AgentRoleTemplateImportService>();

        var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        return new TestContext(connection, provider);
    }

    private sealed class TestContext : IAsyncDisposable
    {
        public TestContext(SqliteConnection connection, ServiceProvider services)
        {
            Connection = connection;
            Services = services;
        }

        public SqliteConnection Connection { get; }

        public ServiceProvider Services { get; }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class FakeManagedSkillStore : IManagedSkillStore
    {
        public FakeManagedSkillStore(IReadOnlyList<ManagedInstalledSkill> installedSkills)
        {
            InstalledSkills = installedSkills;
        }

        public IReadOnlyList<ManagedInstalledSkill> InstalledSkills { get; }

        public Task<IReadOnlyList<ManagedInstalledSkill>> GetInstalledAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(InstalledSkills);

        public Task<ManagedInstalledSkill?> GetByInstallKeyAsync(string installKey, CancellationToken ct = default)
            => Task.FromResult<ManagedInstalledSkill?>(null);

        public Task<ManagedInstalledSkill?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<ManagedInstalledSkill?>(null);

        public Task<ManagedInstalledSkill> InstallAsync(SkillCatalogEntry catalogItem, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<bool> RemoveAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<SkillStoreMaintenanceResult> CheckForUpdatesAsync(CancellationToken ct = default)
            => Task.FromResult(new SkillStoreMaintenanceResult(true, 0, 0, 0, "No-op"));

        public Task<SkillStoreMaintenanceResult> UpdateAsync(CancellationToken ct = default)
            => Task.FromResult(new SkillStoreMaintenanceResult(true, 0, 0, 0, "No-op"));
    }
}
