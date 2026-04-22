using Microsoft.EntityFrameworkCore;
using OpenStaff.Entities;
using OpenStaff.Repositories;

namespace OpenStaff.EntityFrameworkCore;

/// <summary>
/// OpenStaff 基础设施层使用的 EF Core 数据库上下文。
/// The EF Core database context used by the OpenStaff infrastructure layer.
/// </summary>
public class AppDbContext : DbContext, IRepositoryContext
{
    /// <summary>
    /// 初始化数据库上下文。
    /// Initializes the database context.
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>
    /// 调整上下文级运行选项，使本地开发阶段不会因待处理迁移警告而频繁中断。
    /// Adjusts context-level runtime options so local development is not repeatedly interrupted by pending-migration warnings.
    /// </summary>
    /// <param name="optionsBuilder">要补充配置的选项生成器。/ The options builder to augment.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // zh-CN: 开发阶段允许模型与迁移短暂漂移，避免调试时被 PendingModelChangesWarning 频繁打断。
        // en: Allow temporary model/migration drift during development so debugging is not repeatedly interrupted by PendingModelChangesWarning.
        optionsBuilder.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    /// <summary>
    /// 全局设置记录。
    /// Global setting records.
    /// </summary>
    public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();

    /// <summary>
    /// 项目与工作区元数据。
    /// Project and workspace metadata.
    /// </summary>
    public DbSet<Project> Projects => Set<Project>();

    /// <summary>
    /// 可复用的代理角色定义。
    /// Reusable agent role definitions.
    /// </summary>
    public DbSet<AgentRole> AgentRoles => Set<AgentRole>();

    /// <summary>
    /// 项目中的角色关联。
    /// Project-scoped role memberships.
    /// </summary>
    public DbSet<ProjectAgentRole> ProjectAgentRoles => Set<ProjectAgentRole>();

    /// <summary>
    /// 项目任务项。
    /// Project task items.
    /// </summary>
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    /// <summary>
    /// 任务依赖边。
    /// Task dependency edges.
    /// </summary>
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();

    /// <summary>
    /// 项目级代理事件。
    /// Project-level agent events.
    /// </summary>
    public DbSet<AgentEvent> AgentEvents => Set<AgentEvent>();

    /// <summary>
    /// 项目检查点快照。
    /// Project checkpoint snapshots.
    /// </summary>
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();

    /// <summary>
    /// 已安装插件记录。
    /// Installed plugin records.
    /// </summary>
    public DbSet<Plugin> Plugins => Set<Plugin>();

    /// <summary>
    /// 对话会话根记录。
    /// Root chat session records.
    /// </summary>
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();

    /// <summary>
    /// 会话执行帧。
    /// Session execution frames.
    /// </summary>
    public DbSet<ChatFrame> ChatFrames => Set<ChatFrame>();

    /// <summary>
    /// 会话消息内容。
    /// Session message content.
    /// </summary>
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    /// <summary>
    /// 会话事件流条目。
    /// Session event stream entries.
    /// </summary>
    public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();

    /// <summary>
    /// 会话执行包索引。
    /// Session execution-package index.
    /// </summary>
    public DbSet<ExecutionPackage> ExecutionPackages => Set<ExecutionPackage>();

    /// <summary>
    /// 任务与执行包的投影索引。
    /// Projection links between tasks and execution packages.
    /// </summary>
    public DbSet<TaskExecutionLink> TaskExecutionLinks => Set<TaskExecutionLink>();

    /// <summary>
    /// 模型/协议供应商账户。
    /// Model or protocol provider accounts.
    /// </summary>
    public DbSet<ProviderAccount> ProviderAccounts => Set<ProviderAccount>();

    /// <summary>
    /// MCP 服务目录。
    /// MCP server catalog entries.
    /// </summary>
    public DbSet<McpServer> McpServers => Set<McpServer>();

    /// <summary>
    /// 角色测试场景与 MCP 服务器绑定。
    /// Bindings between agent-role test chat and installed MCP servers.
    /// </summary>
    public DbSet<AgentRoleMcpBinding> AgentRoleMcpBindings => Set<AgentRoleMcpBinding>();

    /// <summary>
    /// 角色测试场景与 Skill 绑定。
    /// Bindings between agent-role test chat and managed skills.
    /// </summary>
    public DbSet<AgentRoleSkillBinding> AgentRoleSkillBindings => Set<AgentRoleSkillBinding>();

    /// <summary>
    /// 项目内角色关联与 Skill 绑定。
    /// Bindings between project-scoped role memberships and managed skills.
    /// </summary>
    public DbSet<ProjectAgentRoleSkillBinding> ProjectAgentRoleSkillBindings => Set<ProjectAgentRoleSkillBinding>();

    /// <summary>
    /// 已安装 Skill 记录。
    /// Installed skill records.
    /// </summary>
    public DbSet<InstalledSkill> InstalledSkills => Set<InstalledSkill>();

    /// <summary>
    /// 发现并应用当前程序集中的所有实体映射配置。
    /// Discovers and applies every entity mapping configuration from the current assembly.
    /// </summary>
    /// <param name="modelBuilder">用于构建 EF 模型的对象。/ The builder used to construct the EF model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // zh-CN: EF 会扫描整个程序集中的 IEntityTypeConfiguration 实现，因此配置类所在文件夹或命名空间不会影响映射生效。
        // en: EF scans the entire assembly for IEntityTypeConfiguration implementations, so folder or namespace placement does not prevent a mapping from being applied.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
