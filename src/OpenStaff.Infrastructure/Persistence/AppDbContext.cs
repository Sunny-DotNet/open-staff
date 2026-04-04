using Microsoft.EntityFrameworkCore;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence;

/// <summary>
/// 应用数据库上下文 / Application database context
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();
    public DbSet<ModelProvider> ModelProviders => Set<ModelProvider>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<AgentRole> AgentRoles => Set<AgentRole>();
    public DbSet<ProjectAgent> ProjectAgents => Set<ProjectAgent>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    public DbSet<AgentEvent> AgentEvents => Set<AgentEvent>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
    public DbSet<Plugin> Plugins => Set<Plugin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
