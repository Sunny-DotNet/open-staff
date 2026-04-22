namespace OpenStaff.ApiServices;
/// <summary>
/// 运行态监控应用服务实现。
/// Application service implementation for runtime monitoring.
/// </summary>
public class MonitorApiService : ApiServiceBase, IMonitorApiService
{
    private readonly IProjectRepository _projects;
    private readonly IProjectAgentRoleRepository _projectAgents;
    private readonly ITaskItemRepository _tasks;
    private readonly IAgentEventRepository _agentEvents;
    private readonly IChatSessionRepository _chatSessions;
    private readonly IAgentRoleRepository _agentRoles;
    private readonly ICheckpointRepository _checkpoints;
    private readonly ProviderAccountService _accountService;

    /// <summary>
    /// 初始化运行态监控应用服务。
    /// Initializes the runtime monitoring application service.
    /// </summary>
    public MonitorApiService(
        IProjectRepository projects,
        IProjectAgentRoleRepository projectAgents,
        ITaskItemRepository tasks,
        IAgentEventRepository agentEvents,
        IChatSessionRepository chatSessions,
        IAgentRoleRepository agentRoles,
        ICheckpointRepository checkpoints,
        ProviderAccountService accountService,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _projects = projects;
        _projectAgents = projectAgents;
        _tasks = tasks;
        _agentEvents = agentEvents;
        _chatSessions = chatSessions;
        _agentRoles = agentRoles;
        _checkpoints = checkpoints;
        _accountService = accountService;
    }

    /// <inheritdoc />
    public async Task<SystemStatsDto> GetStatsAsync(CancellationToken ct)
    {
        var projectCount = await _projects.CountAsync(ct);
        var agentCount = await _projectAgents.CountAsync(ct);
        var taskCount = await _tasks.CountAsync(ct);
        var eventCount = await _agentEvents.CountAsync(ct);
        var completedTasks = await _tasks.CountAsync(t => t.Status == "done", ct);
        var sessionCount = await _chatSessions.CountAsync(ct);
        var providerCount = (await _accountService.GetAllAsync()).Count;
        var agentRoleCount = await _agentRoles.CountAsync(ct);

        var recentSessions = await _chatSessions
            .AsNoTracking()
            .Include(s => s.Project)
            .OrderByDescending(s => s.CreatedAt)
            .Take(10)
            .Select(s => new RecentSessionDto
            {
                Id = s.Id,
                ProjectId = s.ProjectId,
                ProjectName = s.Project != null ? s.Project.Name : null,
                Status = s.Status,
                Scene = s.Scene,
                Input = s.InitialInput,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(ct);

        return new SystemStatsDto
        {
            Projects = projectCount,
            Agents = agentCount,
            AgentRoles = agentRoleCount,
            Tasks = taskCount,
            CompletedTasks = completedTasks,
            Events = eventCount,
            Sessions = sessionCount,
            ModelProviders = providerCount,
            RecentSessions = recentSessions
        };
    }

    /// <inheritdoc />
    public async Task<ProjectStatsDto> GetProjectStatsAsync(Guid projectId, CancellationToken ct)
    {
        var project = await _projects.FindAsync(projectId, ct);
        if (project == null) throw new KeyNotFoundException("Project not found");

        var agents = await _projectAgents
            .Where(a => a.ProjectId == projectId)
            .Include(a => a.AgentRole)
            .Select(a => new ProjectAgentDto
            {
                Id = a.Id,
                RoleName = a.AgentRole!.Name,
                Status = a.Status
            })
            .ToListAsync(ct);

        var tasksByStatus = await _tasks
            .Where(t => t.ProjectId == projectId)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var eventsByType = await _agentEvents
            .Where(e => e.ProjectId == projectId)
            .GroupBy(e => e.EventType)
            .Select(g => new { EventType = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var recentEvents = await _agentEvents
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .Include(e => e.ProjectAgentRole)
                .ThenInclude(agent => agent!.AgentRole)
            .OrderByDescending(e => e.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        var sessionCountsByScene = await _chatSessions
            .AsNoTracking()
            .Where(session => session.ProjectId == projectId)
            .GroupBy(session => session.Scene)
            .Select(group => new SceneCountRow(group.Key, group.Count()))
            .ToListAsync(ct);

        var taskMetadata = await _tasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId)
            .Select(task => task.Metadata)
            .ToListAsync(ct);

        var eventMetadata = await _agentEvents
            .AsNoTracking()
            .Where(agentEvent => agentEvent.ProjectId == projectId)
            .Select(agentEvent => new EventMetadataRow(agentEvent.Metadata))
            .ToListAsync(ct);

        var checkpointCount = await _checkpoints
            .CountAsync(c => c.ProjectId == projectId, ct);

        return new ProjectStatsDto
        {
            Agents = agents,
            TasksByStatus = tasksByStatus.ToDictionary(x => x.Status, x => x.Count),
            EventsByType = eventsByType.ToDictionary(x => x.EventType, x => x.Count),
            SceneBreakdown = BuildSceneBreakdown(sessionCountsByScene, taskMetadata, eventMetadata),
            RecentEvents = recentEvents.Select(item => MapEventDto(item)).ToList(),
            Checkpoints = checkpointCount
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<EventDto>> GetEventsAsync(GetEventsRequest request, CancellationToken ct)
    {
        var query = _agentEvents
            .AsNoTracking()
            .Where(e => e.ProjectId == request.ProjectId);

        if (!string.IsNullOrEmpty(request.EventType))
            query = query.Where(e => e.EventType == request.EventType);

        var normalizedScene = RuntimeProjectionMetadataMapper.NormalizeScene(request.Scene);
        if (normalizedScene == null)
        {
            var totalCount = await query.CountAsync(ct);
            var pageEvents = await query
                .Include(e => e.ProjectAgentRole)
                    .ThenInclude(agent => agent!.AgentRole)
                .OrderByDescending(e => e.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            return new PagedResult<EventDto>(
                pageEvents.Select(item => MapEventDto(item)).ToList(),
                totalCount);
        }

        var filteredEvents = (await query
            .Include(e => e.ProjectAgentRole)
                .ThenInclude(agent => agent!.AgentRole)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct))
            .Select(agentEvent => new
            {
                Event = agentEvent,
                Metadata = RuntimeProjectionMetadataMapper.ParseAgentEventMetadata(agentEvent.Metadata)
            })
            .Where(item => string.Equals(
                RuntimeProjectionMetadataMapper.NormalizeScene(item.Metadata?.Scene),
                normalizedScene,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        var total = filteredEvents.Count;
        var events = filteredEvents
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => MapEventDto(item.Event, item.Metadata))
            .ToList();

        return new PagedResult<EventDto>(events, total);
    }

    /// <summary>
    /// Builds per-scene monitoring aggregates by combining session counts, task metadata, and event metadata so the dashboard can show workload, tokens, and latency together.
    /// 通过合并会话计数、任务元数据和事件元数据构建按场景聚合的监控结果，使仪表板能够同时展示工作量、令牌与时延。
    /// </summary>
    /// <param name="sessionCountsByScene">Session totals already grouped by scene. / 已按场景分组的会话总数。</param>
    /// <param name="taskMetadata">Raw task metadata payloads used to recover task scenes. / 用于恢复任务场景的原始任务元数据负载。</param>
    /// <param name="eventMetadata">Event metadata rows used to count events and aggregate runtime metrics. / 用于统计事件并聚合运行时指标的事件元数据行。</param>
    /// <returns>Ordered dashboard breakdown per normalized scene. / 按规范化场景排序的仪表板拆分结果。</returns>
    private static List<SceneBreakdownDto> BuildSceneBreakdown(
        IReadOnlyCollection<SceneCountRow> sessionCountsByScene,
        IReadOnlyCollection<string?> taskMetadata,
        IReadOnlyCollection<EventMetadataRow> eventMetadata)
    {
        // zh-CN: 场景拆分需要同时汇总会话、任务和事件三种投影，才能还原不同场景下的真实工作量。
        // en: Scene breakdown must aggregate session, task, and event projections together to reflect the real workload per scene.
        var accumulators = new Dictionary<string, SceneBreakdownAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var sessionGroup in sessionCountsByScene)
        {
            var scene = RuntimeProjectionMetadataMapper.NormalizeScene(sessionGroup.Scene);
            if (scene == null)
                continue;

            GetOrAddAccumulator(accumulators, scene).SessionCount += sessionGroup.Count;
        }

        foreach (var metadataJson in taskMetadata)
        {
            var metadata = RuntimeProjectionMetadataMapper.ParseTaskMetadata(metadataJson);
            var scene = RuntimeProjectionMetadataMapper.NormalizeScene(metadata?.Scene);
            if (scene == null)
                continue;

            GetOrAddAccumulator(accumulators, scene).TaskCount += 1;
        }

        foreach (var row in eventMetadata)
        {
            var metadata = RuntimeProjectionMetadataMapper.ParseAgentEventMetadata(row.Metadata);
            var scene = RuntimeProjectionMetadataMapper.NormalizeScene(metadata?.Scene);
            if (scene == null)
                continue;

            var accumulator = GetOrAddAccumulator(accumulators, scene);
            accumulator.EventCount += 1;

            if (metadata?.TotalTokens is int totalTokens)
                accumulator.TotalTokens += totalTokens;

            if (metadata?.DurationMs is long durationMs)
            {
                accumulator.RunCount += 1;
                accumulator.DurationTotalMs += durationMs;
            }
        }

        return accumulators.Values
            .OrderBy(item => item.Scene, StringComparer.OrdinalIgnoreCase)
            .Select(item => new SceneBreakdownDto
            {
                Scene = item.Scene,
                SessionCount = item.SessionCount,
                TaskCount = item.TaskCount,
                EventCount = item.EventCount,
                RunCount = item.RunCount,
                TotalTokens = item.TotalTokens,
                AverageDurationMs = item.RunCount == 0
                    ? null
                    : (long)Math.Round((double)item.DurationTotalMs / item.RunCount)
            })
            .ToList();
    }

    /// <summary>
    /// Gets the existing accumulator for a normalized scene or creates one so repeated aggregation passes share the same mutable bucket.
    /// 获取指定规范化场景的累加器，若不存在则创建一个，以便多轮聚合共享同一可变桶。
    /// </summary>
    /// <param name="accumulators">Accumulator dictionary keyed by normalized scene. / 以规范化场景为键的累加器字典。</param>
    /// <param name="scene">Normalized scene name. / 规范化后的场景名称。</param>
    /// <returns>Mutable accumulator for the requested scene. / 对应场景的可变累加器。</returns>
    private static SceneBreakdownAccumulator GetOrAddAccumulator(
        IDictionary<string, SceneBreakdownAccumulator> accumulators,
        string scene)
    {
        if (!accumulators.TryGetValue(scene, out var accumulator))
        {
            accumulator = new SceneBreakdownAccumulator(scene);
            accumulators[scene] = accumulator;
        }

        return accumulator;
    }

    /// <summary>
    /// Maps an agent event and optional pre-parsed metadata into the monitor DTO, avoiding a second metadata parse when the caller already has it.
    /// 将代理事件及可选的预解析元数据映射为监控 DTO；当调用方已解析元数据时可避免再次解析。
    /// </summary>
    /// <param name="agentEvent">Agent event entity to expose. / 要输出的代理事件实体。</param>
    /// <param name="metadata">Optional pre-parsed metadata payload for reuse. / 可复用的预解析元数据负载。</param>
    /// <returns>DTO used by monitoring APIs. / 监控 API 使用的 DTO。</returns>
    private static EventDto MapEventDto(
        Entities.AgentEvent agentEvent,
        AgentEventMetadataPayload? metadata = null)
    {
        metadata ??= RuntimeProjectionMetadataMapper.ParseAgentEventMetadata(agentEvent.Metadata);
        return new EventDto
        {
            Id = agentEvent.Id,
            EventType = agentEvent.EventType,
            Data = agentEvent.Content,
            Content = agentEvent.Content,
            Metadata = agentEvent.Metadata,
            TaskId = metadata?.TaskId,
            SessionId = metadata?.SessionId,
            FrameId = metadata?.FrameId,
            MessageId = metadata?.MessageId,
            ExecutionPackageId = metadata?.ExecutionPackageId,
            ProjectAgentRoleId = agentEvent.ProjectAgentRoleId,
            AgentName = agentEvent.ProjectAgentRole?.AgentRole?.Name,
            Scene = RuntimeProjectionMetadataMapper.NormalizeScene(metadata?.Scene),
            EntryKind = metadata?.EntryKind,
            AgentRoleId = metadata?.AgentRoleId,
            TargetAgentRoleId = metadata?.TargetAgentRoleId,
            TargetProjectAgentRoleId = metadata?.TargetProjectAgentRoleId,
            Model = metadata?.Model,
            ToolName = metadata?.ToolName,
            ToolCallId = metadata?.ToolCallId,
            Status = metadata?.Status,
            SourceFrameId = metadata?.SourceFrameId,
            SourceEffectIndex = metadata?.SourceEffectIndex,
            Detail = metadata?.Detail,
            Attempt = metadata?.Attempt,
            MaxAttempts = metadata?.MaxAttempts,
            TotalTokens = metadata?.TotalTokens,
            DurationMs = metadata?.DurationMs,
            FirstTokenMs = metadata?.FirstTokenMs,
            CreatedAt = agentEvent.CreatedAt
        };
    }

    private sealed record SceneCountRow(string Scene, int Count);

    private sealed record EventMetadataRow(string? Metadata);

    private sealed class SceneBreakdownAccumulator
    {
        /// <inheritdoc />
        public SceneBreakdownAccumulator(string scene)
        {
            Scene = scene;
        }

        public string Scene { get; }
        public int SessionCount { get; set; }
        public int TaskCount { get; set; }
        public int EventCount { get; set; }
        public int RunCount { get; set; }
        public int TotalTokens { get; set; }
        public long DurationTotalMs { get; set; }
    }
}





