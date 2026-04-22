using OpenStaff.Entities;
using OpenStaff.Repositories;

namespace OpenStaff.EntityFrameworkCore.Repositories;

public sealed class GlobalSettingRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<GlobalSetting, Guid>(dbContext), IGlobalSettingRepository;
public sealed class ProjectRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<Project, Guid>(dbContext), IProjectRepository;
public sealed class AgentRoleRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<AgentRole, Guid>(dbContext), IAgentRoleRepository;
public sealed class ProjectAgentRoleRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<ProjectAgentRole, Guid>(dbContext), IProjectAgentRoleRepository;
public sealed class TaskItemRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<TaskItem, Guid>(dbContext), ITaskItemRepository;
public sealed class TaskDependencyRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<TaskDependency, Guid>(dbContext), ITaskDependencyRepository;
public sealed class AgentEventRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<AgentEvent, Guid>(dbContext), IAgentEventRepository;
public sealed class CheckpointRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<Checkpoint, Guid>(dbContext), ICheckpointRepository;
public sealed class PluginRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<Plugin, Guid>(dbContext), IPluginRepository;
public sealed class ChatSessionRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<ChatSession, Guid>(dbContext), IChatSessionRepository;
public sealed class ChatFrameRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<ChatFrame, Guid>(dbContext), IChatFrameRepository;
public sealed class ChatMessageRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<ChatMessage, Guid>(dbContext), IChatMessageRepository;
public sealed class SessionEventRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<SessionEvent, Guid>(dbContext), ISessionEventRepository;
public sealed class ExecutionPackageRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<ExecutionPackage, Guid>(dbContext), IExecutionPackageRepository;
public sealed class TaskExecutionLinkRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<TaskExecutionLink, Guid>(dbContext), ITaskExecutionLinkRepository;
public sealed class ProviderAccountRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<ProviderAccount, Guid>(dbContext), IProviderAccountRepository;
public sealed class McpServerRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<McpServer, Guid>(dbContext), IMcpServerRepository;
public sealed class AgentRoleMcpBindingRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<AgentRoleMcpBinding, Guid>(dbContext), IAgentRoleMcpBindingRepository;
public sealed class AgentRoleSkillBindingRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<AgentRoleSkillBinding, Guid>(dbContext), IAgentRoleSkillBindingRepository;
public sealed class ProjectAgentRoleSkillBindingRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<ProjectAgentRoleSkillBinding, Guid>(dbContext), IProjectAgentRoleSkillBindingRepository;
public sealed class InstalledSkillRepository(AppDbContext dbContext) : EntityFrameworkCoreRepository<InstalledSkill, Guid>(dbContext), IInstalledSkillRepository;
