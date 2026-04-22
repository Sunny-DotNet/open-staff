using System.Collections.Concurrent;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent;
using OpenStaff.Agent.Builtin;
using OpenStaff.Agent.Services;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Agents;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.Core.Notifications;
using OpenStaff.Core.Orchestration;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Orchestration.Services;
/// <summary>
/// 编排服务，负责项目级智能体缓存、角色解析与运行时预热。
/// Orchestration service that manages project-scoped agent caching, role resolution, and runtime warm-up.
/// </summary>
public class OrchestrationService : IOrchestrator, IProjectAgentRuntimeCache
{
    private readonly AgentFactory _agentFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAgentMcpToolService _agentMcpToolService;
    private readonly INotificationService _notification;
    private readonly ILogger<OrchestrationService> _logger;

    // zh-CN: 缓存键由项目和场景共同决定，避免不同场景复用带有错误上下文的运行时实例。
    // en: The cache key is scoped by both project and scene so different scenes do not reuse runtime instances with stale context.
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, (IStaffAgent agent, DateTime lastUsed)>> _projectAgents = new();

    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// 初始化编排服务。
    /// Initializes the orchestration service.
    /// </summary>
    public OrchestrationService(
        AgentFactory agentFactory,
        IServiceScopeFactory scopeFactory,
        IAgentMcpToolService agentMcpToolService,
        INotificationService notification,
        ILogger<OrchestrationService> logger)
    {
        _agentFactory = agentFactory;
        _scopeFactory = scopeFactory;
        _agentMcpToolService = agentMcpToolService;
        _notification = notification;
        _logger = logger;
    }

    /// <summary>
    /// 预热项目内可立即使用的智能体运行时。
    /// Warms up the agent runtimes that should be immediately available for a project.
    /// </summary>
    public async Task InitializeProjectAgentsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing agents for project {ProjectId}", projectId);

        var agents = _projectAgents.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, (IStaffAgent, DateTime)>());
        var warmupRoles = await LoadWarmupRolesAsync(projectId, cancellationToken);
        foreach (var role in warmupRoles)
        {
            await GetOrCreateAgentAsync(projectId, role, scene: null, cancellationToken);
        }

        await _notification.NotifyAsync(Channels.Project(projectId), EventTypes.SystemNotice, new
        {
            content = $"已初始化 {agents.Count} 个智能体角色"
        }, cancellationToken);

        _ = Task.Run(() => CleanupInactiveAgentsAsync(cancellationToken));
    }

    /// <summary>
    /// 获取当前缓存中的智能体状态快照。
    /// Gets a snapshot of the agent states currently held in cache.
    /// </summary>
    public Task<IReadOnlyList<AgentStatusInfo>> GetAgentStatusesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var result = new List<AgentStatusInfo>();

        if (_projectAgents.TryGetValue(projectId, out var agents))
        {
            foreach (var (roleType, agentData) in agents)
            {
                var resolvedRoleType = ExtractRoleType(roleType);
                result.Add(new AgentStatusInfo
                {
                    RoleType = resolvedRoleType,
                    RoleName = resolvedRoleType,
                    Status = AgentStatus.Idle,
                    LastUsed = agentData.lastUsed
                });
            }
        }

        return Task.FromResult<IReadOnlyList<AgentStatusInfo>>(result);
    }

    /// <summary>
    /// 获取项目和场景范围内的智能体运行时，必要时创建并写入缓存。
    /// Gets the project-and-scene-scoped agent runtime, creating and caching it when necessary.
    /// </summary>
    /// <param name="projectId">项目标识。/ Project identifier.</param>
    /// <param name="role">请求的角色信息。/ Requested role information.</param>
    /// <param name="scene">可选的运行场景。/ Optional runtime scene.</param>
    /// <param name="cancellationToken">取消令牌。/ Cancellation token.</param>
    /// <returns>可复用的智能体运行时；创建失败时返回 <see langword="null" />。/ A reusable agent runtime, or <see langword="null" /> when creation fails.</returns>
    /// <remarks>
    /// zh-CN: 该方法会命中或填充项目级缓存，并在缓存命中时刷新最后使用时间，以支持后续空闲清理。
    /// en: This method reads from or populates the project-level cache and refreshes the last-used timestamp on cache hits so later idle cleanup can make lifecycle decisions.
    /// </remarks>
    private async Task<IStaffAgent?> GetOrCreateAgentAsync(
        Guid projectId,
        AgentRole role,
        SceneType? scene,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetAgentCacheKey(role.Name, scene);

        var agents = _projectAgents.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, (IStaffAgent, DateTime)>());

        if (agents.TryGetValue(cacheKey, out var existingData))
        {
            agents.TryUpdate(cacheKey, (existingData.agent, DateTime.UtcNow), existingData);
            return existingData.agent;
        }

        try
        {
            var (resolvedRole, project, projectAgent) = await LoadRoleContextAsync(projectId, role.Name, cancellationToken);
            if (!resolvedRole.ModelProviderId.HasValue)
                throw new InvalidOperationException($"Role '{role.Name}' must bind a provider account.");
            if (string.IsNullOrWhiteSpace(resolvedRole.ModelName))
                throw new InvalidOperationException($"Role '{role.Name}' must bind a model.");

            var agentContext = new AgentContext
            {
                ProjectId = projectId,
                Project = project,
                Language = project.Language,
                Role = resolvedRole,
                Scene = scene,
            };

            // zh-CN: 内置提供器需要额外加载 MCP 工具绑定，其余提供器沿用通用工厂逻辑。
            // en: Builtin providers must load MCP tool bindings explicitly; all other providers use the shared factory path.
            IStaffAgent agent;
            var providerType = resolvedRole.ProviderType ?? "builtin";
            if (string.Equals(providerType, "builtin", StringComparison.OrdinalIgnoreCase)
                && _agentFactory.Providers.GetValueOrDefault("builtin") is BuiltinAgentProvider builtinProvider)
            {
                var mcpTools = await _agentMcpToolService.LoadEnabledToolsAsync(
                    new AgentMcpToolLoadContext(
                        MessageScene.ProjectGroup,
                        projectAgent?.Id,
                        null),
                    cancellationToken);
                agent = await builtinProvider.CreateAgentAsync(resolvedRole, agentContext, mcpTools);
            }
            else
            {
                agent = await _agentFactory.CreateAgentAsync(resolvedRole, agentContext);
            }

            agents.TryAdd(cacheKey, (agent, DateTime.UtcNow));
            _logger.LogDebug("Created agent {RoleType} for project {ProjectId}", role.Name, projectId);
            return agent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create agent {RoleType} for project {ProjectId}", role.Name, projectId);
            return null;
        }
    }

    /// <summary>
    /// 从数据库加载创建运行时所需的项目和角色上下文快照。
    /// Loads the project and role context snapshot required to create a runtime.
    /// </summary>
    /// <param name="projectId">项目标识。/ Project identifier.</param>
    /// <param name="roleType">角色类型。/ Role type.</param>
    /// <param name="cancellationToken">取消令牌。/ Cancellation token.</param>
    /// <returns>用于运行时初始化的角色与项目实体。/ The role and project entities used for runtime initialization.</returns>
    /// <remarks>
    /// zh-CN: 查询使用 AsNoTracking 读取当前持久化状态，确保缓存创建基于只读快照而不会把实体附着到长期生命周期中。
    /// en: The query uses AsNoTracking to read the current persisted state, ensuring cache creation is based on a read-only snapshot rather than long-lived tracked entities.
    /// </remarks>
    private async Task<IReadOnlyList<AgentRole>> LoadWarmupRolesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        using var repositories = CreateRoleContextScope();

        var project = await repositories.Projects
            .AsNoTracking()
            .Include(item => item.AgentRoles)
                .ThenInclude(binding => binding.AgentRole)
            .FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project '{projectId}' not found");

        var projectRoles = project.AgentRoles
            .Select(binding => binding.AgentRole)
            .Where(role => role is { IsActive: true })
            .DistinctBy(role => role!.Id)
            .Cast<AgentRole>()
            .ToList();
        if (projectRoles.Count > 0)
            return projectRoles;

        var fallbackRole = await repositories.AgentRoles
            .AsNoTracking()
            .Where(role => role.IsActive)
            .OrderByDescending(role => role.IsBuiltin)
            .ThenBy(role => role.Name)
            .Take(1)
            .ToListAsync(cancellationToken);
        return fallbackRole;
    }

    private async Task<(AgentRole Role, Project Project, ProjectAgentRole? ProjectAgentRole)> LoadRoleContextAsync(
        Guid projectId,
        string? requestedRoleName,
        CancellationToken cancellationToken)
    {
        using var repositories = CreateRoleContextScope();

        var project = await repositories.Projects
            .AsNoTracking()
            .Include(p => p.AgentRoles)
                .ThenInclude(agent => agent.AgentRole)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project '{projectId}' not found");

        var projectAgent = project.AgentRoles.FirstOrDefault(binding =>
            binding.AgentRole is { IsActive: true }
            && (string.IsNullOrWhiteSpace(requestedRoleName)
                || string.Equals(binding.AgentRole.Name, requestedRoleName, StringComparison.OrdinalIgnoreCase)));
        if (projectAgent?.AgentRole != null)
            return (projectAgent.AgentRole, project, projectAgent);

        IQueryable<AgentRole> roleQuery = repositories.AgentRoles
            .AsNoTracking()
            .Where(role => role.IsActive);
        if (!string.IsNullOrWhiteSpace(requestedRoleName))
            roleQuery = roleQuery.Where(role => role.Name == requestedRoleName);

        var role = await roleQuery
            .OrderByDescending(item => item.IsBuiltin)
            .ThenBy(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await repositories.AgentRoles
                .AsNoTracking()
                .Where(role => role.IsActive)
                .OrderByDescending(item => item.IsBuiltin)
                .ThenBy(item => item.Name)
                .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Role is not available");

        return (role, project, null);
    }

    /// <summary>
    /// 为角色上下文加载创建短生命周期仓储作用域，避免单例服务直接依赖 Scoped 仓储实例。
    /// Creates a short-lived repository scope for loading role context so the singleton service does not capture scoped repository instances.
    /// </summary>
    private RoleContextScope CreateRoleContextScope()
    {
        var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        return new RoleContextScope(
            scope,
            services.GetRequiredService<IProjectRepository>(),
            services.GetRequiredService<IAgentRoleRepository>());
    }

    /// <summary>
    /// 清理长时间未使用的项目智能体缓存。
    /// Cleans up project-agent cache entries that have been idle for a long time.
    /// </summary>
    /// <param name="cancellationToken">取消令牌。/ Cancellation token.</param>
    /// <returns>表示后台清理过程的任务。/ A task representing the background cleanup pass.</returns>
    /// <remarks>
    /// zh-CN: 该清理仅影响内存中的运行时缓存，不释放数据库记录；是否移除完全取决于最后使用时间与清理阈值。
    /// en: This cleanup affects only in-memory runtime cache entries and does not delete database records; removal is driven solely by last-used timestamps and the cleanup threshold.
    /// </remarks>
    private async Task CleanupInactiveAgentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow - _cleanupInterval;

            foreach (var (projectId, agents) in _projectAgents)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // zh-CN: 清理仅移除长期未使用的缓存实例，不会影响数据库中的角色定义。
                // en: Cleanup removes only long-idle cached runtimes and never mutates persisted role definitions.
                var toRemove = agents
                    .Where(kv => kv.Value.lastUsed < cutoffTime)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var roleType in toRemove)
                {
                    if (agents.TryRemove(roleType, out var removed))
                    {
                        await removed.agent.DisposeAsync();
                        _logger.LogInformation("Removed inactive agent {RoleType} for project {ProjectId}", roleType, projectId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during agent cleanup");
        }
    }

    /// <summary>
    /// 组合角色类型与场景生成稳定的缓存键。
    /// Combines role type and scene into a stable cache key.
    /// </summary>
    /// <param name="roleType">角色类型。/ Role type.</param>
    /// <param name="scene">可选场景。/ Optional scene.</param>
    /// <returns>用于项目内运行时缓存的键。/ Cache key used for project-scoped runtimes.</returns>
    /// <remarks>
    /// zh-CN: 当场景存在时会附加 <c>::</c> 分隔符，避免不同场景复用同一角色缓存。
    /// en: When a scene is present, the method appends the <c>::</c> separator so distinct scenes do not reuse the same role cache entry.
    /// </remarks>
    private static string GetAgentCacheKey(string roleType, SceneType? scene) =>
        scene.HasValue ? $"{roleType}::{scene.Value}" : roleType;

    /// <summary>
    /// 失效指定项目中的角色缓存。
    /// Invalidates the cached runtimes for a role within a project.
    /// </summary>
    public void InvalidateProjectAgent(Guid projectId, string roleType)
    {
        if (!_projectAgents.TryGetValue(projectId, out var agents))
            return;

        var cacheKeys = agents.Keys
            .Where(key => string.Equals(ExtractRoleType(key), roleType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var cacheKey in cacheKeys)
        {
            if (agents.TryRemove(cacheKey, out var removed))
                _ = removed.agent.DisposeAsync().AsTask();
        }
    }

    /// <summary>
    /// 从缓存键中提取原始角色类型。
    /// Extracts the original role type from a cache key.
    /// </summary>
    /// <param name="cacheKey">缓存键。/ Cache key.</param>
    /// <returns>去除场景后缀后的角色类型。/ The role type with any scene suffix removed.</returns>
    /// <remarks>
    /// zh-CN: 该解析与 <see cref="GetAgentCacheKey" /> 的 <c>::</c> 约定配套使用，便于按角色批量失效不同场景的缓存。
    /// en: This parser complements the <c>::</c> convention in <see cref="GetAgentCacheKey" /> so callers can invalidate all scene-specific cache entries for a role together.
    /// </remarks>
    private static string ExtractRoleType(string cacheKey)
    {
        var separator = cacheKey.IndexOf("::", StringComparison.Ordinal);
        return separator >= 0 ? cacheKey[..separator] : cacheKey;
    }

    /// <summary>
    /// 聚合编排服务读取角色上下文所需的显式仓储。
    /// Groups the explicit repositories required by the orchestration service to load role context.
    /// </summary>
    private sealed class RoleContextScope : IDisposable
    {
        private readonly IServiceScope _scope;

        public RoleContextScope(
            IServiceScope scope,
            IProjectRepository projects,
            IAgentRoleRepository agentRoles)
        {
            _scope = scope;
            Projects = projects;
            AgentRoles = agentRoles;
        }

        public IProjectRepository Projects { get; }

        public IAgentRoleRepository AgentRoles { get; }

        public void Dispose() => _scope.Dispose();
    }
}

