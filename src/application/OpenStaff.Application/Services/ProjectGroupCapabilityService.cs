using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent.Services;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Sessions.Services;
/// <summary>
/// ProjectGroup 能力授权结果。
/// Result produced when evaluating a ProjectGroup capability request.
/// </summary>
/// <param name="CanRetryWithoutPenalty">是否可以立即无惩罚重试。 / Whether execution can retry immediately without penalty.</param>
/// <param name="MissingTools">仍缺失的工具列表。 / Tools that are still missing.</param>
/// <param name="Detail">补充说明。 / Additional detail.</param>
public sealed record ProjectGroupCapabilityGrantResult(
    bool CanRetryWithoutPenalty,
    IReadOnlyList<string> MissingTools,
    string? Detail = null);

/// <summary>
/// ProjectGroup 能力授权服务，负责刷新角色可用工具并决定是否允许重试。
/// ProjectGroup capability service that refreshes role tooling and decides whether execution may retry.
/// </summary>
public sealed class ProjectGroupCapabilityService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAgentMcpToolService _agentMcpToolService;
    private readonly IProjectAgentRuntimeCache _runtimeCache;
    private readonly ILogger<ProjectGroupCapabilityService> _logger;

    /// <summary>
    /// 初始化 ProjectGroup 能力授权服务。
    /// Initializes the ProjectGroup capability service.
    /// </summary>
    public ProjectGroupCapabilityService(
        IServiceScopeFactory scopeFactory,
        IAgentMcpToolService agentMcpToolService,
        IProjectAgentRuntimeCache runtimeCache,
        ILogger<ProjectGroupCapabilityService> logger)
    {
        _scopeFactory = scopeFactory;
        _agentMcpToolService = agentMcpToolService;
        _runtimeCache = runtimeCache;
        _logger = logger;
    }

    /// <summary>
    /// 尝试为当前帧准备能力补齐后的重试条件。
    /// Attempts to prepare the retry conditions after a capability request is approved.
    /// </summary>
    public async Task<ProjectGroupCapabilityGrantResult> TryPrepareCapabilityRetryAsync(
        Guid projectId,
        Guid frameId,
        ProjectGroupCapabilityRequest request,
        CancellationToken ct)
    {
        var requiredTools = request.RequiredTools
            .Where(tool => !string.IsNullOrWhiteSpace(tool))
            .Select(tool => tool.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requiredTools.Count == 0)
            return new ProjectGroupCapabilityGrantResult(false, [], "未提供可授权的能力标识。");

        using var repositories = CreateCapabilityScope();

        var frame = await repositories.ChatFrames
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == frameId, ct);
        if (frame?.TaskId == null)
            return new ProjectGroupCapabilityGrantResult(false, requiredTools, "当前任务没有关联可恢复的执行上下文。");

        var task = await repositories.Tasks
            .Include(item => item.AssignedProjectAgentRole)
                .ThenInclude(agent => agent!.AgentRole)
            .FirstOrDefaultAsync(item => item.Id == frame.TaskId.Value, ct);
        if (task?.AssignedProjectAgentRole?.AgentRole == null)
            return new ProjectGroupCapabilityGrantResult(false, requiredTools, "当前任务没有关联可授权的目标智能体。");

        var role = task.AssignedProjectAgentRole.AgentRole;
        if (!IsBuiltinProvider(role))
        {
            return new ProjectGroupCapabilityGrantResult(
                false,
                requiredTools,
                "当前仅支持内置/自定义角色自动刷新能力，Vendor 角色仍需手动补齐配置。");
        }

        // zh-CN: 旧的内置本地工具注册链已经移除，这里只通过 MCP 绑定能力来满足运行时请求。
        // en: The legacy builtin local-tool registry has been removed, so runtime capability requests are now satisfied through MCP bindings only.
        var satisfied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mcpGrant = await _agentMcpToolService.EnsureToolsAllowedAsync(task.AssignedProjectAgentRole.Id, requiredTools, ct);
        foreach (var tool in mcpGrant.SatisfiedTools)
            satisfied.Add(tool);

        var missing = requiredTools
            .Where(tool => !satisfied.Contains(tool))
            .ToList();
        if (missing.Count > 0)
        {
            if (mcpGrant.Changed)
                _runtimeCache.InvalidateProjectAgent(projectId, role.Name);

            return new ProjectGroupCapabilityGrantResult(
                false,
                missing,
                "当前角色没有对应的已绑定工具来源，请先补齐工具配置或 MCP 绑定。");
        }

        // zh-CN: MCP 能力发生变化后，必须失效项目级运行时缓存，确保后续重试拿到最新工具集。
        // en: Invalidate the project-scoped runtime cache after MCP capability changes so the retry sees the latest tool set.
        _runtimeCache.InvalidateProjectAgent(projectId, role.Name);

        repositories.AgentEvents.Add(new AgentEvent
        {
            ProjectId = projectId,
            ProjectAgentRoleId = task.AssignedProjectAgentRoleId,
            EventType = EventTypes.CapabilityApproved,
            Content = $"已批准能力申请：{string.Join(", ", requiredTools)}",
            Metadata = System.Text.Json.JsonSerializer.Serialize(new AgentEventMetadataPayload
            {
                TaskId = task.Id,
                FrameId = frameId,
                Scene = SessionSceneTypes.ProjectGroup,
                Status = "approved",
                Source = "capability_approval",
                Detail = string.Join(", ", requiredTools)
            })
        });
        await repositories.RepositoryContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Approved capability request for project {ProjectId}, role {RoleType}, tools {Tools}",
            projectId,
            role.Name,
            string.Join(", ", requiredTools));

        return new ProjectGroupCapabilityGrantResult(true, []);
    }

    /// <summary>
    /// 为能力授权流程创建短生命周期持久化作用域，集中解析显式仓储与提交上下文。
    /// Creates a short-lived persistence scope for capability approval so explicit repositories and the save context are resolved together.
    /// </summary>
    private CapabilityScope CreateCapabilityScope()
    {
        var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        return new CapabilityScope(
            scope,
            services.GetRequiredService<IChatFrameRepository>(),
            services.GetRequiredService<ITaskItemRepository>(),
            services.GetRequiredService<IAgentEventRepository>(),
            services.GetRequiredService<IRepositoryContext>());
    }

    /// <summary>
    /// 判断角色是否使用可自动刷新 MCP 能力的内置提供程序。
    /// Determines whether the role uses a builtin provider that is eligible for automatic MCP capability refresh.
    /// </summary>
    private static bool IsBuiltinProvider(AgentRole role) =>
        string.IsNullOrWhiteSpace(role.ProviderType)
        || string.Equals(role.ProviderType, "builtin", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 聚合能力授权流程所需的显式仓储与提交上下文。
    /// Groups the explicit repositories and save context required by the capability-approval flow.
    /// </summary>
    private sealed class CapabilityScope : IDisposable
    {
        private readonly IServiceScope _scope;

        public CapabilityScope(
            IServiceScope scope,
            IChatFrameRepository chatFrames,
            ITaskItemRepository tasks,
            IAgentEventRepository agentEvents,
            IRepositoryContext repositoryContext)
        {
            _scope = scope;
            ChatFrames = chatFrames;
            Tasks = tasks;
            AgentEvents = agentEvents;
            RepositoryContext = repositoryContext;
        }

        public IChatFrameRepository ChatFrames { get; }

        public ITaskItemRepository Tasks { get; }

        public IAgentEventRepository AgentEvents { get; }

        public IRepositoryContext RepositoryContext { get; }

        public void Dispose() => _scope.Dispose();
    }
}

