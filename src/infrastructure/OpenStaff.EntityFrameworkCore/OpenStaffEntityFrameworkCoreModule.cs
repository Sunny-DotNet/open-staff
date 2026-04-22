using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.EntityFrameworkCore.Repositories;
using OpenStaff.Repositories;

namespace OpenStaff;

/// <summary>
/// EntityFrameworkCore module. Hosts the EF Core context, repositories, mappings, and migrations.
/// </summary>
[DependsOn(typeof(OpenStaffCoreModule), typeof(OpenStaffDomainModule))]
public class OpenStaffEntityFrameworkCoreModule : OpenStaffModule
{
    /// <summary>
    /// Registers the shared database context using the same fallback chain that the old infrastructure module owned.
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Configuration;
        var staffDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".staff");
        Directory.CreateDirectory(staffDir);

        var defaultDbPath = Path.Combine(staffDir, "openstaff.db");
        var connectionString = configuration.GetConnectionString("openstaff")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? $"Data Source={defaultDbPath}";

        context.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        context.Services.AddScoped<IRepositoryContext>(sp => sp.GetRequiredService<AppDbContext>());
        context.Services.AddScoped(typeof(IRepository<,>), typeof(EntityFrameworkCoreRepository<,>));
        context.Services.AddScoped<IGlobalSettingRepository, GlobalSettingRepository>();
        context.Services.AddScoped<IProjectRepository, ProjectRepository>();
        context.Services.AddScoped<IAgentRoleRepository, AgentRoleRepository>();
        context.Services.AddScoped<IProjectAgentRoleRepository, ProjectAgentRoleRepository>();
        context.Services.AddScoped<ITaskItemRepository, TaskItemRepository>();
        context.Services.AddScoped<ITaskDependencyRepository, TaskDependencyRepository>();
        context.Services.AddScoped<IAgentEventRepository, AgentEventRepository>();
        context.Services.AddScoped<ICheckpointRepository, CheckpointRepository>();
        context.Services.AddScoped<IPluginRepository, PluginRepository>();
        context.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
        context.Services.AddScoped<IChatFrameRepository, ChatFrameRepository>();
        context.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        context.Services.AddScoped<ISessionEventRepository, SessionEventRepository>();
        context.Services.AddScoped<IExecutionPackageRepository, ExecutionPackageRepository>();
        context.Services.AddScoped<ITaskExecutionLinkRepository, TaskExecutionLinkRepository>();
        context.Services.AddScoped<IProviderAccountRepository, ProviderAccountRepository>();
        context.Services.AddScoped<IMcpServerRepository, McpServerRepository>();
        context.Services.AddScoped<IAgentRoleMcpBindingRepository, AgentRoleMcpBindingRepository>();
        context.Services.AddScoped<IAgentRoleSkillBindingRepository, AgentRoleSkillBindingRepository>();
        context.Services.AddScoped<IProjectAgentRoleSkillBindingRepository, ProjectAgentRoleSkillBindingRepository>();
        context.Services.AddScoped<IInstalledSkillRepository, InstalledSkillRepository>();
    }
}
