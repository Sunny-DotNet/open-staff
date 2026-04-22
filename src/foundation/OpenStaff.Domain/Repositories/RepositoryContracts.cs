using OpenStaff.Entities;

namespace OpenStaff.Repositories;

public interface IGlobalSettingRepository : IRepository<GlobalSetting, Guid>;
public interface IProjectRepository : IRepository<Project, Guid>;
public interface IAgentRoleRepository : IRepository<AgentRole, Guid>;
public interface IProjectAgentRoleRepository : IRepository<ProjectAgentRole, Guid>;
public interface ITaskItemRepository : IRepository<TaskItem, Guid>;
public interface ITaskDependencyRepository : IRepository<TaskDependency, Guid>;
public interface IAgentEventRepository : IRepository<AgentEvent, Guid>;
public interface ICheckpointRepository : IRepository<Checkpoint, Guid>;
public interface IPluginRepository : IRepository<Plugin, Guid>;
public interface IChatSessionRepository : IRepository<ChatSession, Guid>;
public interface IChatFrameRepository : IRepository<ChatFrame, Guid>;
public interface IChatMessageRepository : IRepository<ChatMessage, Guid>;
public interface ISessionEventRepository : IRepository<SessionEvent, Guid>;
public interface IExecutionPackageRepository : IRepository<ExecutionPackage, Guid>;
public interface ITaskExecutionLinkRepository : IRepository<TaskExecutionLink, Guid>;
public interface IProviderAccountRepository : IRepository<ProviderAccount, Guid>;
public interface IMcpServerRepository : IRepository<McpServer, Guid>;
public interface IAgentRoleMcpBindingRepository : IRepository<AgentRoleMcpBinding, Guid>;
public interface IAgentRoleSkillBindingRepository : IRepository<AgentRoleSkillBinding, Guid>;
public interface IProjectAgentRoleSkillBindingRepository : IRepository<ProjectAgentRoleSkillBinding, Guid>;
public interface IInstalledSkillRepository : IRepository<InstalledSkill, Guid>;
