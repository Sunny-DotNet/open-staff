using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenStaff.Application.Seeding.Services;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Repositories;

namespace OpenStaff.Tests.Unit;

public class JobTitleNormalizationSeedServiceTests
{
    [Fact]
    public async Task StartAsync_ShouldNormalizeLocalizedJobTitlesToKeys()
    {
        await using var testContext = await CreateTestContextAsync();

        await using (var seedScope = testContext.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AgentRoles.AddRange(
                new AgentRole
                {
                    Id = Guid.NewGuid(),
                    Name = "Monica",
                    JobTitle = "项目秘书",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                },
                new AgentRole
                {
                    Id = Guid.NewGuid(),
                    Name = "Jennifer",
                    JobTitle = "软件工程师",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            await db.SaveChangesAsync();
        }

        var service = new JobTitleNormalizationSeedService(
            testContext.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobTitleNormalizationSeedService>.Instance);

        await service.StartAsync(CancellationToken.None);

        await using var verifyScope = testContext.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var titles = await verifyDb.AgentRoles
            .OrderBy(item => item.Name)
            .Select(item => item.JobTitle)
            .ToListAsync();

        Assert.Equal(["software_engineer", "secretary"], titles);
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
}
