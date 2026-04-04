using Microsoft.EntityFrameworkCore;
using OpenStaff.Core.Models;

namespace OpenStaff.Infrastructure.Persistence;

/// <summary>
/// 应用数据库上下文 / Application database context
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        // 临时抑制迁移警告，用于开发调试
        optionsBuilder.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<AgentRole> AgentRoles => Set<AgentRole>();
    public DbSet<ProjectAgent> ProjectAgents => Set<ProjectAgent>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    public DbSet<AgentEvent> AgentEvents => Set<AgentEvent>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
    public DbSet<Plugin> Plugins => Set<Plugin>();

    // 对话系统
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatFrame> ChatFrames => Set<ChatFrame>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
