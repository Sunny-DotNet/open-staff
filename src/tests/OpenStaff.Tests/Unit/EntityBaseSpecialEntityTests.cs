using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;

namespace OpenStaff.Tests.Unit;

public sealed class EntityBaseSpecialEntityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public EntityBaseSpecialEntityTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options);

        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task TaskDependencyRepository_FindAsync_UsesGeneratedId_AndNaturalKeyRemainsUnique()
    {
        var project = new Project { Name = "Task Dependency Project" };
        var prerequisite = new TaskItem { Project = project, Title = "Prerequisite" };
        var dependent = new TaskItem { Project = project, Title = "Dependent" };

        _db.AddRange(project, prerequisite, dependent);

        var dependency = new TaskDependency
        {
            Task = dependent,
            DependsOn = prerequisite
        };

        _db.TaskDependencies.Add(dependency);
        await _db.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, dependency.Id);

        var repository = new TaskDependencyRepository(_db);
        var reloaded = await repository.FindAsync(dependency.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(dependent.Id, reloaded!.TaskId);
        Assert.Equal(prerequisite.Id, reloaded.DependsOnId);

        _db.TaskDependencies.Add(new TaskDependency
        {
            TaskId = dependent.Id,
            DependsOnId = prerequisite.Id
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task AgentRoleMcpBindingRepository_FindAsync_UsesGeneratedId_AndNaturalKeyRemainsUnique()
    {
        var role = new AgentRole
        {
            Name = "Role",
            JobTitle = "role"
        };

        var server = new McpServer
        {
            Name = "Server",
            Category = McpCategories.General,
            TransportType = McpTransportTypes.Stdio,
            Mode = McpServerModes.Local,
            Source = McpSources.Builtin
        };

        _db.AddRange(role, server);

        var binding = new AgentRoleMcpBinding
        {
            AgentRole = role,
            McpServer = server,
            ToolFilter = "[\"tool-a\"]"
        };

        _db.AgentRoleMcpBindings.Add(binding);
        await _db.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, binding.Id);

        var repository = new AgentRoleMcpBindingRepository(_db);
        var reloaded = await repository.FindAsync(binding.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(role.Id, reloaded!.AgentRoleId);
        Assert.Equal(server.Id, reloaded.McpServerId);

        _db.AgentRoleMcpBindings.Add(new AgentRoleMcpBinding
        {
            AgentRoleId = role.Id,
            McpServerId = server.Id
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
