using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Application.Conversations.Models;
using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Sessions.Services;
/// <summary>
/// ProjectGroup 执行服务，负责提及解析、排队和任务租约流转。
/// ProjectGroup execution service that resolves mentions, manages queues, and advances task leases.
/// </summary>
public sealed class ProjectGroupExecutionService
{
    private static readonly Regex MentionRegex = new(@"@(?<target>[^\s,，:：]+)", RegexOptions.Compiled);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectGroupExecutionService> _logger;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _agentLocks = new();

    /// <summary>
    /// 初始化 ProjectGroup 执行服务。
    /// Initializes the ProjectGroup execution service.
    /// </summary>
    public ProjectGroupExecutionService(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectGroupExecutionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 根据用户输入解析实际分发目标。
    /// Resolves the actual dispatch target from user input.
    /// </summary>
    public Task<ProjectGroupDispatchTarget> ResolveDispatchTargetAsync(
        Guid projectId,
        string input,
        IReadOnlyList<ConversationMention>? mentions,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Task.FromResult(BuildSecretaryTarget(input, input, false, dispatchSource: "project_group_user_input"));
        }

        var mentionedTarget = TryResolveMentionTarget(input, mentions);
        return Task.FromResult(BuildSecretaryTarget(
            input,
            input,
            !string.IsNullOrWhiteSpace(mentionedTarget),
            mentionedTarget,
            dispatchSource: "project_group_user_input"));
    }

    public Task<ProjectGroupDispatchTarget> ResolveDispatchTargetAsync(Guid projectId, string input, CancellationToken ct)
        => ResolveDispatchTargetAsync(projectId, input, mentions: null, ct);

    /// <summary>
    /// 解析 ProjectGroup 回复中的任务分发包。
    /// Extracts the shared task-dispatch envelope emitted inside ProjectGroup replies.
    /// </summary>
    public bool TryExtractDispatch(
        string? content,
        out string displayContent,
        out IReadOnlyList<ProjectGroupDispatchInstruction> dispatches)
    {
        if (!ProjectGroupDispatchEnvelope.TryExtract(content, out displayContent, out var envelope))
        {
            dispatches = [];
            return false;
        }

        dispatches = envelope.Dispatches;
        return true;
    }

    /// <summary>
    /// 解析隐藏项目模型的统一结构化结果。
    /// Extracts the unified structured result emitted by the hidden project orchestrator.
    /// </summary>
    public bool TryExtractOrchestratorResult(
        string? content,
        out ProjectGroupOrchestratorResult? result)
    {
        return ProjectGroupOrchestratorContract.TryParse(content, out result);
    }

    /// <summary>
    /// 兼容原有秘书分发提取入口。
    /// Compatibility wrapper for the original secretary-dispatch extraction entry point.
    /// </summary>
    public bool TryExtractSecretaryDispatch(
        string? content,
        out string displayContent,
        out IReadOnlyList<ProjectGroupDispatchInstruction> dispatches)
        => TryExtractDispatch(content, out displayContent, out dispatches);

    /// <summary>
    /// 解析角色返回的能力申请包。
    /// Extracts the capability-request envelope returned by a role.
    /// </summary>
    public bool TryExtractCapabilityRequest(
        string? content,
        out string displayContent,
        out ProjectGroupCapabilityRequest? request)
    {
        if (!ProjectGroupCapabilityRequestEnvelope.TryExtract(content, out displayContent, out var envelope))
        {
            request = null;
            return false;
        }

        request = envelope.Request;
        return request != null;
    }

    /// <summary>
    /// 把秘书的单个分发指令解析为实际执行目标。
    /// Resolves a single secretary dispatch instruction into an executable target.
    /// </summary>
    public async Task<ProjectGroupDispatchTarget> ResolveSecretaryDispatchAsync(
        Guid projectId,
        ProjectGroupDispatchInstruction instruction,
        CancellationToken ct)
        => await ResolveDispatchAsync(projectId, instruction, "project_group_secretary_dispatch", ct);

    /// <summary>
    /// 解析任意 ProjectGroup 成员返回的分发指令。
    /// Resolves a ProjectGroup dispatch instruction into an executable target using the supplied dispatch source.
    /// </summary>
    public async Task<ProjectGroupDispatchTarget> ResolveDispatchAsync(
        Guid projectId,
        ProjectGroupDispatchInstruction instruction,
        string dispatchSource,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(instruction.TargetRole))
        {
            return new ProjectGroupDispatchTarget(
                BuiltinRoleTypes.Secretary,
                instruction.Task,
                instruction.Task,
                false,
                MentionedTarget: instruction.TargetRole,
                UnresolvedMessage: "秘书返回的任务分发缺少 targetRole，已跳过。");
        }

        if (string.IsNullOrWhiteSpace(instruction.Task))
        {
            return new ProjectGroupDispatchTarget(
                BuiltinRoleTypes.Secretary,
                instruction.Task,
                instruction.Task,
                false,
                MentionedTarget: instruction.TargetRole,
                UnresolvedMessage: $"秘书给 {instruction.TargetRole} 的任务说明为空，已跳过。");
        }

        var projectAgents = await GetProjectAgentsAsync(projectId, ct);
        var matchedAgent = projectAgents.FirstOrDefault(agent => MatchesAgent(agent, instruction.TargetRole));
        if (matchedAgent == null)
        {
            return new ProjectGroupDispatchTarget(
                BuiltinRoleTypes.Secretary,
                instruction.Task,
                instruction.Task,
                false,
                MentionedTarget: instruction.TargetRole,
                UnresolvedMessage: $"秘书尝试分派给 {instruction.TargetRole}，但该智能体当前不在项目成员中。");
        }

        return new ProjectGroupDispatchTarget(
            GetTargetRole(matchedAgent.AgentRole!),
            instruction.Task.Trim(),
            instruction.Task.Trim(),
            false,
            matchedAgent.Id,
            instruction.TargetRole,
            DispatchSource: dispatchSource);
    }

    /// <summary>
    /// 解析秘书分发指令，必要时展开为广播目标列表。
    /// Resolves a secretary dispatch instruction and expands it into broadcast targets when required.
    /// </summary>
    public async Task<IReadOnlyList<ProjectGroupDispatchTarget>> ResolveSecretaryDispatchesAsync(
        Guid projectId,
        ProjectGroupDispatchInstruction instruction,
        CancellationToken ct)
        => await ResolveDispatchesAsync(projectId, instruction, "project_group_secretary_dispatch", ct);

    /// <summary>
    /// 解析 ProjectGroup 分发指令，必要时展开为广播目标列表。
    /// Resolves a ProjectGroup dispatch instruction and expands it into broadcast targets when required.
    /// </summary>
    public async Task<IReadOnlyList<ProjectGroupDispatchTarget>> ResolveDispatchesAsync(
        Guid projectId,
        ProjectGroupDispatchInstruction instruction,
        string dispatchSource,
        CancellationToken ct)
    {
        if (IsBroadcastTarget(instruction.TargetRole))
        {
            var projectAgents = await GetProjectAgentsAsync(projectId, ct);
            var targets = projectAgents
                .Where(agent => agent.AgentRole != null)
                .Select(agent => new ProjectGroupDispatchTarget(
                    GetTargetRole(agent.AgentRole!),
                    instruction.Task.Trim(),
                    instruction.Task.Trim(),
                    false,
                    agent.Id,
                    instruction.TargetRole,
                    DispatchSource: ToBroadcastDispatchSource(dispatchSource)))
                .ToList();

            if (targets.Count == 0)
            {
                return
                [
                    new ProjectGroupDispatchTarget(
                        BuiltinRoleTypes.Secretary,
                        instruction.Task,
                        instruction.Task,
                        false,
                        MentionedTarget: instruction.TargetRole,
                        UnresolvedMessage: "秘书尝试向所有人分发任务，但当前项目还没有已分配的智能体。")
                ];
            }

            return targets;
        }

        return [await ResolveDispatchAsync(projectId, instruction, dispatchSource, ct)];
    }

    /// <summary>
    /// 为指定目标创建并排队 ProjectGroup 任务。
    /// Creates and queues a ProjectGroup task for the specified target.
    /// </summary>
    public async Task<ProjectGroupQueueResult> QueueTaskAsync(
        Guid projectId,
        Guid sessionId,
        Guid frameId,
        ProjectGroupDispatchTarget target,
        CancellationToken ct)
    {
        if (target.ProjectAgentRoleId == null)
            throw new InvalidOperationException("ProjectGroup task queue requires a resolved project agent");

        // zh-CN: 每个项目成员只允许串行消费任务，避免同一智能体在多个会话帧中并发写入状态。
        // en: Each project member consumes tasks serially so the same agent never mutates runtime state from multiple frames at once.
        var agentLock = _agentLocks.GetOrAdd(target.ProjectAgentRoleId.Value, _ => new SemaphoreSlim(1, 1));
        await agentLock.WaitAsync(ct);

        try
        {
            using var repositories = CreateExecutionScope();

            var projectAgent = await repositories.ProjectAgentRoles
                .Include(agent => agent.AgentRole)
                .FirstOrDefaultAsync(agent => agent.Id == target.ProjectAgentRoleId.Value, ct)
                ?? throw new InvalidOperationException($"Project agent {target.ProjectAgentRoleId} not found");

            var frame = await repositories.ChatFrames.FirstOrDefaultAsync(f => f.Id == frameId, ct)
                ?? throw new InvalidOperationException($"Frame {frameId} not found");

            var task = new TaskItem
            {
                ProjectId = projectId,
                Title = BuildTaskTitle(target.Purpose),
                Description = target.UserMessageContent,
                AssignedProjectAgentRoleId = projectAgent.Id,
                Status = TaskItemStatus.Pending,
                Metadata = JsonSerializer.Serialize(new TaskItemRuntimeMetadata
                {
                    SessionId = sessionId,
                    FrameId = frameId,
                    Scene = SessionSceneTypes.ProjectGroup,
                    Source = target.DispatchSource,
                    MentionedProjectAgentRoleId = target.ProjectAgentRoleId,
                    TargetProjectAgentRoleId = target.ProjectAgentRoleId,
                    TargetRoleName = target.TargetRole,
                    LastStatus = TaskItemStatus.Pending
                })
            };

            repositories.Tasks.Add(task);
            frame.TaskId = task.Id;

            var activeTask = await FindActiveTaskAsync(repositories.Tasks, projectAgent.Id, ct);
            if (activeTask == null && (IsBusyStatus(projectAgent.Status) || !string.IsNullOrWhiteSpace(projectAgent.CurrentTask)))
            {
                ResetAgentAvailability(projectAgent);
            }

            var hasRunningTask = activeTask != null;

            await AppendAgentEventAsync(
                repositories.AgentEvents,
                projectId,
                projectAgent.Id,
                EventTypes.TaskAssigned,
                $"已收到任务：{task.Title}",
                new { taskId = task.Id, frameId, sessionId, scene = SessionSceneTypes.ProjectGroup, targetRole = target.TargetRole });

            if (hasRunningTask)
            {
                await AppendAgentEventAsync(
                    repositories.AgentEvents,
                    projectId,
                    projectAgent.Id,
                    EventTypes.TaskQueued,
                    $"任务排队中：{task.Title}",
                    new { taskId = task.Id, frameId, sessionId, scene = SessionSceneTypes.ProjectGroup, targetRole = target.TargetRole });

                await repositories.RepositoryContext.SaveChangesAsync(ct);

                return new ProjectGroupQueueResult(
                    task.Id,
                    target.TargetRole,
                    $"收到，我先完成手头任务，随后处理：{task.Title}",
                    Lease: null);
            }

            MarkTaskStarted(projectAgent, task);
            if (TryReadTaskMetadata(task.Metadata) is { } startedMetadata)
            {
                startedMetadata.LastStatus = TaskItemStatus.InProgress;
                task.Metadata = JsonSerializer.Serialize(startedMetadata);
            }
            await AppendAgentEventAsync(
                repositories.AgentEvents,
                projectId,
                projectAgent.Id,
                EventTypes.TaskStarted,
                $"开始处理任务：{task.Title}",
                new { taskId = task.Id, frameId, sessionId, scene = SessionSceneTypes.ProjectGroup, targetRole = target.TargetRole });

            await repositories.RepositoryContext.SaveChangesAsync(ct);

            return new ProjectGroupQueueResult(
                task.Id,
                target.TargetRole,
                QueueAcknowledgement: null,
                Lease: BuildLease(projectId, sessionId, frameId, projectAgent, task));
        }
        finally
        {
            agentLock.Release();
        }
    }

    /// <summary>
    /// 完成当前任务并决定是否继续后续租约。
    /// Completes the current task and decides whether a follow-up lease should continue execution.
    /// </summary>
    public async Task<ProjectGroupTaskCompletionResult> CompleteTaskAsync(
        ProjectGroupExecutionLease lease,
        bool success,
        bool allowRetry,
        string result,
        CancellationToken ct,
        string? secretaryNoticeOverride = null,
        bool retryWithoutPenalty = false)
    {
        var agentLock = _agentLocks.GetOrAdd(lease.ProjectAgentRoleId, _ => new SemaphoreSlim(1, 1));
        await agentLock.WaitAsync(ct);

        try
        {
            using var repositories = CreateExecutionScope();

            var projectAgent = await repositories.ProjectAgentRoles
                .Include(agent => agent.AgentRole)
                .FirstOrDefaultAsync(agent => agent.Id == lease.ProjectAgentRoleId, ct)
                ?? throw new InvalidOperationException($"Project agent {lease.ProjectAgentRoleId} not found");

            var task = await repositories.Tasks.FirstOrDefaultAsync(item => item.Id == lease.TaskId, ct)
                ?? throw new InvalidOperationException($"Task {lease.TaskId} not found");
            var metadata = ReadTaskMetadata(task.Metadata, lease);

            if (success)
            {
                task.Status = TaskItemStatus.Done;
                task.CompletedAt = DateTime.UtcNow;
                metadata.LastStatus = TaskItemStatus.Done;
                metadata.LastResult = result;
                metadata.LastError = null;
                await AppendAgentEventAsync(
                    repositories.AgentEvents,
                    lease.ProjectId,
                    projectAgent.Id,
                    EventTypes.TaskCompleted,
                    $"任务完成：{task.Title}",
                    new { taskId = task.Id, frameId = lease.FrameId, sessionId = lease.SessionId, scene = SessionSceneTypes.ProjectGroup, result });
            }
            else
            {
                await AppendAgentEventAsync(
                    repositories.AgentEvents,
                    lease.ProjectId,
                    projectAgent.Id,
                    EventTypes.TaskFailed,
                    $"任务失败：{task.Title}",
                    new
                    {
                        taskId = task.Id,
                        frameId = lease.FrameId,
                        sessionId = lease.SessionId,
                        scene = SessionSceneTypes.ProjectGroup,
                        result,
                        attempt = metadata.AttemptCount + 1
                    });

                // zh-CN: 重试时优先保留当前任务租约，确保同一智能体继续在原上下文中补偿执行。
                // en: Retries keep the existing lease whenever possible so the same agent can continue in the original context.
                if (allowRetry && (retryWithoutPenalty || metadata.AttemptCount + 1 < TaskItemRuntimeMetadata.MaxAttempts))
                {
                    task.Status = TaskItemStatus.InProgress;
                    task.UpdatedAt = DateTime.UtcNow;
                    projectAgent.Status = AgentStatus.Working;
                    projectAgent.CurrentTask = task.Title;
                    projectAgent.UpdatedAt = DateTime.UtcNow;
                    metadata.LastStatus = TaskItemStatus.InProgress;
                    metadata.LastError = result;

                    if (!retryWithoutPenalty)
                    {
                        metadata.AttemptCount += 1;

                        await AppendAgentEventAsync(
                            repositories.AgentEvents,
                            lease.ProjectId,
                            projectAgent.Id,
                            EventTypes.TaskRetry,
                            $"任务重试（第 {metadata.AttemptCount + 1} 次）：{task.Title}",
                            new
                            {
                                taskId = task.Id,
                                frameId = lease.FrameId,
                                sessionId = lease.SessionId,
                                scene = SessionSceneTypes.ProjectGroup,
                                attempt = metadata.AttemptCount + 1,
                                maxAttempts = TaskItemRuntimeMetadata.MaxAttempts
                            });
                    }

                    task.Metadata = JsonSerializer.Serialize(metadata);
                    await repositories.RepositoryContext.SaveChangesAsync(ct);
                    return new ProjectGroupTaskCompletionResult(lease, SecretaryNotice: secretaryNoticeOverride);
                }

                task.Status = TaskItemStatus.Blocked;
                metadata.LastStatus = TaskItemStatus.Blocked;
                metadata.LastError = result;
            }

            task.Metadata = JsonSerializer.Serialize(metadata);
            task.UpdatedAt = DateTime.UtcNow;
            projectAgent.Status = AgentStatus.Idle;
            projectAgent.CurrentTask = null;
            projectAgent.UpdatedAt = DateTime.UtcNow;
            var failureNotice = success
                ? null
                : secretaryNoticeOverride ?? $"{lease.TargetRole} 未能完成任务“{task.Title}”，秘书将重新评估下一步。";

            var nextTask = await repositories.Tasks
                .Where(item => item.AssignedProjectAgentRoleId == lease.ProjectAgentRoleId && item.Status == TaskItemStatus.Pending)
                .OrderBy(item => item.Priority)
                .ThenBy(item => item.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (nextTask == null)
            {
                await repositories.RepositoryContext.SaveChangesAsync(ct);
                return new ProjectGroupTaskCompletionResult(NextLease: null, SecretaryNotice: failureNotice);
            }

            var nextFrame = await repositories.ChatFrames
                .FirstOrDefaultAsync(frame => frame.TaskId == nextTask.Id && frame.Status == FrameStatus.Active, ct);

            if (nextFrame == null)
            {
                await repositories.RepositoryContext.SaveChangesAsync(ct);
                _logger.LogWarning("Queued ProjectGroup task {TaskId} has no active frame, leaving pending", nextTask.Id);
                return new ProjectGroupTaskCompletionResult(NextLease: null, SecretaryNotice: failureNotice);
            }

            MarkTaskStarted(projectAgent, nextTask);
            if (TryReadTaskMetadata(nextTask.Metadata) is { } nextTaskMetadata)
            {
                nextTaskMetadata.LastStatus = TaskItemStatus.InProgress;
                nextTask.Metadata = JsonSerializer.Serialize(nextTaskMetadata);
            }
            await AppendAgentEventAsync(
                repositories.AgentEvents,
                lease.ProjectId,
                projectAgent.Id,
                EventTypes.TaskStarted,
                $"开始处理排队任务：{nextTask.Title}",
                new { taskId = nextTask.Id, frameId = nextFrame.Id, sessionId = nextFrame.SessionId, scene = SessionSceneTypes.ProjectGroup, targetRole = projectAgent.AgentRole!.Id });

            await repositories.RepositoryContext.SaveChangesAsync(ct);

            return new ProjectGroupTaskCompletionResult(
                BuildLease(lease.ProjectId, nextFrame.SessionId, nextFrame.Id, projectAgent, nextTask),
                SecretaryNotice: failureNotice);
        }
        finally
        {
            agentLock.Release();
        }
    }

    /// <summary>
    /// 手动恢复已阻塞的 ProjectGroup 任务。
    /// Manually resumes a blocked ProjectGroup task.
    /// </summary>
    public async Task<ProjectGroupTaskResumeResult?> ResumeBlockedTaskAsync(
        Guid projectId,
        Guid taskId,
        CancellationToken ct)
    {
        using var repositories = CreateExecutionScope();

        var task = await repositories.Tasks
            .Include(item => item.AssignedProjectAgentRole)
                .ThenInclude(agent => agent!.AgentRole)
            .FirstOrDefaultAsync(item => item.Id == taskId && item.ProjectId == projectId, ct);
        if (task?.AssignedProjectAgentRole?.AgentRole == null || task.Status != TaskItemStatus.Blocked)
            return null;

        var metadata = TryReadTaskMetadata(task.Metadata);
        if (metadata?.SessionId is not Guid sessionId || metadata.FrameId is not Guid frameId)
            return null;

        var frame = await repositories.ChatFrames.FirstOrDefaultAsync(item => item.Id == frameId, ct);
        if (frame == null)
            return null;

        var agentLock = _agentLocks.GetOrAdd(task.AssignedProjectAgentRoleId!.Value, _ => new SemaphoreSlim(1, 1));
        await agentLock.WaitAsync(ct);

        try
        {
            MarkTaskStarted(task.AssignedProjectAgentRole, task);
            task.CompletedAt = null;
            task.UpdatedAt = DateTime.UtcNow;
            metadata.LastStatus = TaskItemStatus.InProgress;
            task.Metadata = JsonSerializer.Serialize(metadata);

            await AppendAgentEventAsync(
                repositories.AgentEvents,
                projectId,
                task.AssignedProjectAgentRole.Id,
                EventTypes.TaskRetry,
                $"任务恢复执行：{task.Title}",
                new
                {
                    taskId = task.Id,
                    frameId,
                    sessionId,
                    scene = SessionSceneTypes.ProjectGroup,
                    reason = "manual_resume"
                });

            await repositories.RepositoryContext.SaveChangesAsync(ct);

            return new ProjectGroupTaskResumeResult(
                BuildLease(projectId, sessionId, frameId, task.AssignedProjectAgentRole, task));
        }
        finally
        {
            agentLock.Release();
        }
    }

    /// <summary>
    /// 恢复项目群里因状态脏写或宿主中断而遗留的执行租约。
    /// Recovers stale ProjectGroup leases left behind by dirty agent state or interrupted hosts.
    /// </summary>
    public async Task<IReadOnlyList<ProjectGroupExecutionLease>> RecoverStaleLeasesAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> projectAgentRoleIds,
        CancellationToken ct)
    {
        var recovered = new List<ProjectGroupExecutionLease>();
        foreach (var projectAgentRoleId in projectAgentRoleIds.Distinct())
        {
            var lease = await RecoverStaleLeaseAsync(projectId, projectAgentRoleId, ct);
            if (lease != null)
            {
                recovered.Add(lease);
            }
        }

        return recovered;
    }

    /// <summary>
    /// 使用角色类型、名称或职位匹配显式提及，只有项目成员具备角色信息时才会命中。
    /// Matches an explicit mention against role type, name, or job title and only succeeds when the project member has role metadata.
    /// </summary>
    private static bool MatchesAgent(ProjectAgentRole agent, string mentionedTarget)
    {
        var role = agent.AgentRole;
        if (role == null)
            return false;

        return string.Equals(role.Name, mentionedTarget, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                AgentJobTitleCatalog.NormalizeKey(role.JobTitle),
                AgentJobTitleCatalog.NormalizeKey(mentionedTarget),
                StringComparison.OrdinalIgnoreCase);
    }

    private static string GetTargetRole(AgentRole role)
        => !string.IsNullOrWhiteSpace(role.JobTitle)
            ? AgentJobTitleCatalog.NormalizeKey(role.JobTitle) ?? role.JobTitle
            : role.Name;

    private static ProjectGroupDispatchTarget BuildSecretaryTarget(
        string purpose,
        string userMessageContent,
        bool hasExplicitMention,
        string? mentionedTarget = null,
        string? unresolvedMessage = null,
        string dispatchSource = "project_group_mention")
        => new(
            BuiltinRoleTypes.Secretary,
            purpose,
            userMessageContent,
            hasExplicitMention,
            MentionedTarget: mentionedTarget,
            UnresolvedMessage: unresolvedMessage,
            DispatchSource: dispatchSource);

    /// <summary>
    /// 识别应回退到秘书路由的秘书别名提及。
    /// Recognizes secretary aliases that should route the message back through the secretary workflow.
    /// </summary>
    /// <summary>
    /// 识别广播目标标记，以便分发逻辑进入面向全部项目成员的分支。
    /// Detects broadcast target markers so dispatch can fan out to every project agent.
    /// </summary>
    private static bool IsBroadcastTarget(string? targetRole) =>
        string.Equals(targetRole, "所有人", StringComparison.OrdinalIgnoreCase)
        || string.Equals(targetRole, "all", StringComparison.OrdinalIgnoreCase)
        || string.Equals(targetRole, "@all", StringComparison.OrdinalIgnoreCase);

    private static string ToBroadcastDispatchSource(string dispatchSource)
    {
        if (string.IsNullOrWhiteSpace(dispatchSource))
            return dispatchSource;

        const string dispatchSuffix = "_dispatch";
        return dispatchSource.EndsWith(dispatchSuffix, StringComparison.Ordinal)
            ? $"{dispatchSource[..^dispatchSuffix.Length]}_broadcast"
            : dispatchSource;
    }

    /// <summary>
    /// 读取项目已分配的成员及其角色信息，供提及解析、广播和重试恢复复用。
    /// Loads project agents with role data for mention resolution, broadcast routing, and retry recovery.
    /// </summary>
    private async Task<List<ProjectAgentRole>> GetProjectAgentsAsync(Guid projectId, CancellationToken ct)
    {
        using var repositories = CreateExecutionScope();
        return await repositories.ProjectAgentRoles
            .AsNoTracking()
            .Include(agent => agent.AgentRole)
            .Where(agent => agent.ProjectId == projectId)
            .ToListAsync(ct);
    }

    /// <summary>
    /// 为 ProjectGroup 执行流程创建短生命周期持久化作用域，集中解析显式仓储与提交上下文。
    /// Creates a short-lived persistence scope for ProjectGroup execution so explicit repositories and the save context are resolved together.
    /// </summary>
    private ExecutionScope CreateExecutionScope()
    {
        var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        return new ExecutionScope(
            scope,
            services.GetRequiredService<IProjectAgentRoleRepository>(),
            services.GetRequiredService<IChatFrameRepository>(),
            services.GetRequiredService<ITaskItemRepository>(),
            services.GetRequiredService<IAgentEventRepository>(),
            services.GetRequiredService<IRepositoryContext>());
    }

    private static string? TryResolveMentionTarget(
        string input,
        IReadOnlyList<ConversationMention>? mentions)
    {
        var structuredMention = mentions?.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.RawText));
        if (structuredMention != null)
        {
            var structuredTarget = structuredMention.RawText.Trim().TrimStart('@').Trim();
            return string.IsNullOrWhiteSpace(structuredTarget) ? null : structuredTarget;
        }

        var match = MentionRegex.Match(input);
        if (!match.Success)
        {
            return null;
        }

        var mentionedTarget = match.Groups["target"].Value.Trim();
        return string.IsNullOrWhiteSpace(mentionedTarget) ? null : mentionedTarget;
    }

    /// <summary>
    /// 将执行目的规范化为单行任务标题，并截断到队列展示允许的长度。
    /// Normalizes the execution purpose into a single-line task title and truncates it for queue display.
    /// </summary>
    private static string BuildTaskTitle(string purpose)
    {
        var normalized = purpose.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        if (normalized.Length <= 80)
        {
            return normalized;
        }

        return normalized[..80].TrimEnd() + "...";
    }

    /// <summary>
    /// 同步更新任务与项目成员状态为执行中，并写入当前任务标题。
    /// Marks both the task and project agent as running and records the active task title.
    /// </summary>
    private static void MarkTaskStarted(ProjectAgentRole projectAgent, TaskItem task)
    {
        task.Status = TaskItemStatus.InProgress;
        task.UpdatedAt = DateTime.UtcNow;
        projectAgent.Status = AgentStatus.Working;
        projectAgent.CurrentTask = task.Title;
        projectAgent.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<ProjectGroupExecutionLease?> RecoverStaleLeaseAsync(
        Guid projectId,
        Guid projectAgentRoleId,
        CancellationToken ct)
    {
        var agentLock = _agentLocks.GetOrAdd(projectAgentRoleId, _ => new SemaphoreSlim(1, 1));
        await agentLock.WaitAsync(ct);

        try
        {
            using var repositories = CreateExecutionScope();

            var projectAgent = await repositories.ProjectAgentRoles
                .Include(agent => agent.AgentRole)
                .FirstOrDefaultAsync(agent => agent.Id == projectAgentRoleId && agent.ProjectId == projectId, ct);
            if (projectAgent?.AgentRole == null)
            {
                return null;
            }

            var activeTask = await FindActiveTaskAsync(repositories.Tasks, projectAgent.Id, ct);
            if (activeTask != null)
            {
                var activeFrame = await repositories.ChatFrames
                    .FirstOrDefaultAsync(frame => frame.TaskId == activeTask.Id && frame.Status == FrameStatus.Active, ct);
                if (activeFrame == null)
                {
                    _logger.LogWarning(
                        "ProjectGroup task {TaskId} for agent {ProjectAgentRoleId} is in progress without an active frame; leaving task as-is",
                        activeTask.Id,
                        projectAgent.Id);
                    return null;
                }

                MarkTaskStarted(projectAgent, activeTask);
                if (TryReadTaskMetadata(activeTask.Metadata) is { } activeMetadata)
                {
                    activeMetadata.LastStatus = TaskItemStatus.InProgress;
                    activeTask.Metadata = JsonSerializer.Serialize(activeMetadata);
                }

                await repositories.RepositoryContext.SaveChangesAsync(ct);
                return BuildLease(projectId, activeFrame.SessionId, activeFrame.Id, projectAgent, activeTask);
            }

            var pendingTask = await FindNextPendingTaskAsync(repositories.Tasks, projectAgent.Id, ct);
            if (pendingTask != null)
            {
                var pendingFrame = await repositories.ChatFrames
                    .FirstOrDefaultAsync(frame => frame.TaskId == pendingTask.Id && frame.Status == FrameStatus.Active, ct);
                if (pendingFrame != null)
                {
                    MarkTaskStarted(projectAgent, pendingTask);
                    if (TryReadTaskMetadata(pendingTask.Metadata) is { } pendingMetadata)
                    {
                        pendingMetadata.LastStatus = TaskItemStatus.InProgress;
                        pendingTask.Metadata = JsonSerializer.Serialize(pendingMetadata);
                    }

                    await AppendAgentEventAsync(
                        repositories.AgentEvents,
                        projectId,
                        projectAgent.Id,
                        EventTypes.TaskStarted,
                        $"恢复排队任务：{pendingTask.Title}",
                        new
                        {
                            taskId = pendingTask.Id,
                            frameId = pendingFrame.Id,
                            sessionId = pendingFrame.SessionId,
                            scene = SessionSceneTypes.ProjectGroup,
                            targetRole = GetTargetRole(projectAgent.AgentRole),
                            reason = "stale_recovery"
                        });

                    await repositories.RepositoryContext.SaveChangesAsync(ct);
                    return BuildLease(projectId, pendingFrame.SessionId, pendingFrame.Id, projectAgent, pendingTask);
                }
            }

            if (IsBusyStatus(projectAgent.Status) || !string.IsNullOrWhiteSpace(projectAgent.CurrentTask))
            {
                ResetAgentAvailability(projectAgent);
                await repositories.RepositoryContext.SaveChangesAsync(ct);
            }

            return null;
        }
        finally
        {
            agentLock.Release();
        }
    }

    private static bool IsBusyStatus(string? status)
        => string.Equals(status, AgentStatus.Working, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, AgentStatus.Thinking, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, AgentStatus.Paused, StringComparison.OrdinalIgnoreCase);

    private static void ResetAgentAvailability(ProjectAgentRole projectAgent)
    {
        projectAgent.Status = AgentStatus.Idle;
        projectAgent.CurrentTask = null;
        projectAgent.UpdatedAt = DateTime.UtcNow;
    }

    private static Task<TaskItem?> FindActiveTaskAsync(
        ITaskItemRepository tasks,
        Guid projectAgentRoleId,
        CancellationToken ct)
        => tasks
            .Where(item => item.AssignedProjectAgentRoleId == projectAgentRoleId && item.Status == TaskItemStatus.InProgress)
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(ct);

    private static Task<TaskItem?> FindNextPendingTaskAsync(
        ITaskItemRepository tasks,
        Guid projectAgentRoleId,
        CancellationToken ct)
        => tasks
            .Where(item => item.AssignedProjectAgentRoleId == projectAgentRoleId && item.Status == TaskItemStatus.Pending)
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// 基于当前任务和成员快照构造执行租约，供后续帧续跑和恢复上下文使用。
    /// Builds an execution lease from the current task and agent snapshot for later frame continuation and recovery.
    /// </summary>
    private static ProjectGroupExecutionLease BuildLease(
        Guid projectId,
        Guid sessionId,
        Guid frameId,
        ProjectAgentRole projectAgent,
        TaskItem task)
    {
        var metadata = TaskItemRuntimeMetadata.TryParse(task.Metadata);
        return new ProjectGroupExecutionLease(
            projectId,
            sessionId,
            frameId,
            task.Id,
            projectAgent.Id,
            GetTargetRole(projectAgent.AgentRole!),
            task.Title,
            metadata?.Source ?? "project_group_mention");
    }

    /// <summary>
    /// 向当前 DbContext 追加智能体事件记录但不立即提交，调用方负责统一保存。
    /// Adds an agent-event record to the current DbContext without saving immediately; the caller persists it in the surrounding unit of work.
    /// </summary>
    private static Task AppendAgentEventAsync(
        IAgentEventRepository agentEvents,
        Guid projectId,
        Guid agentId,
        string eventType,
        string content,
        object metadata)
    {
        agentEvents.Add(new AgentEvent
        {
            ProjectId = projectId,
            ProjectAgentRoleId = agentId,
            EventType = eventType,
            Content = content,
            Metadata = JsonSerializer.Serialize(metadata)
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// 读取任务运行时元数据；缺失时使用当前租约补齐最小路由上下文。
    /// Reads task runtime metadata and synthesizes minimal routing context from the current lease when metadata is missing.
    /// </summary>
    private static TaskItemRuntimeMetadata ReadTaskMetadata(string? metadataJson, ProjectGroupExecutionLease lease)
    {
        var metadata = TryReadTaskMetadata(metadataJson);
        if (metadata != null)
            return metadata;

        return new TaskItemRuntimeMetadata
        {
            SessionId = lease.SessionId,
            FrameId = lease.FrameId,
            Scene = SessionSceneTypes.ProjectGroup,
            Source = "project_group_task",
            TargetProjectAgentRoleId = lease.ProjectAgentRoleId
        };
    }

    /// <summary>
    /// 尝试解析任务运行时元数据，解析规则委托给共享映射器并允许返回空值。
    /// Attempts to parse task runtime metadata through the shared mapper and returns null when no usable payload can be produced.
    /// </summary>
    private static TaskItemRuntimeMetadata? TryReadTaskMetadata(string? metadataJson)
        => RuntimeProjectionMetadataMapper.ParseTaskMetadata(metadataJson);

    /// <summary>
    /// 聚合 ProjectGroup 执行流程所需的显式仓储与提交上下文。
    /// Groups the explicit repositories and save context required by the ProjectGroup execution flow.
    /// </summary>
    private sealed class ExecutionScope : IDisposable
    {
        private readonly IServiceScope _scope;

        public ExecutionScope(
            IServiceScope scope,
            IProjectAgentRoleRepository projectAgents,
            IChatFrameRepository chatFrames,
            ITaskItemRepository tasks,
            IAgentEventRepository agentEvents,
            IRepositoryContext repositoryContext)
        {
            _scope = scope;
            ProjectAgentRoles = projectAgents;
            ChatFrames = chatFrames;
            Tasks = tasks;
            AgentEvents = agentEvents;
            RepositoryContext = repositoryContext;
        }

        public IProjectAgentRoleRepository ProjectAgentRoles { get; }

        public IChatFrameRepository ChatFrames { get; }

        public ITaskItemRepository Tasks { get; }

        public IAgentEventRepository AgentEvents { get; }

        public IRepositoryContext RepositoryContext { get; }

        public void Dispose() => _scope.Dispose();
    }
}

/// <summary>
/// ProjectGroup 分发目标。
/// Dispatch target resolved for a ProjectGroup message.
/// </summary>
/// <param name="TargetRole">目标角色。 / Target role.</param>
/// <param name="Purpose">规范化后的执行目的。 / Normalized execution purpose.</param>
/// <param name="UserMessageContent">保留给审计和展示的原始用户消息。 / Original user message retained for display and audit.</param>
/// <param name="HasExplicitMention">是否来自显式提及。 / Whether the target came from an explicit mention.</param>
/// <param name="ProjectAgentRoleId">解析出的项目成员标识。 / Resolved project-agent identifier.</param>
/// <param name="MentionedTarget">原始提及文本。 / Original mention text.</param>
/// <param name="UnresolvedMessage">无法解析时返回给用户的说明。 / Message returned to the user when resolution fails.</param>
/// <param name="DispatchSource">分发来源标识。 / Dispatch source identifier.</param>
public sealed record ProjectGroupDispatchTarget(
    string TargetRole,
    string Purpose,
    string UserMessageContent,
    bool HasExplicitMention,
    Guid? ProjectAgentRoleId = null,
    string? MentionedTarget = null,
    string? UnresolvedMessage = null,
    string DispatchSource = "project_group_mention");

/// <summary>
/// 任务排队结果。
/// Result returned after queueing a ProjectGroup task.
/// </summary>
/// <param name="TaskId">任务标识。 / Task identifier.</param>
/// <param name="TargetRole">目标角色。 / Target role.</param>
/// <param name="QueueAcknowledgement">排队提示消息。 / Queue acknowledgement message.</param>
/// <param name="Lease">可立即执行时返回的租约。 / Lease returned when execution can start immediately.</param>
public sealed record ProjectGroupQueueResult(
    Guid TaskId,
    string TargetRole,
    string? QueueAcknowledgement,
    ProjectGroupExecutionLease? Lease);

/// <summary>
/// ProjectGroup 执行租约。
/// Execution lease for a ProjectGroup task.
/// </summary>
/// <param name="ProjectId">项目标识。 / Project identifier.</param>
/// <param name="SessionId">会话标识。 / Session identifier.</param>
/// <param name="FrameId">帧标识。 / Frame identifier.</param>
/// <param name="TaskId">任务标识。 / Task identifier.</param>
/// <param name="ProjectAgentRoleId">项目成员标识。 / Project-agent identifier.</param>
/// <param name="TargetRole">目标角色。 / Target role.</param>
/// <param name="TaskTitle">任务标题。 / Task title.</param>
public sealed record ProjectGroupExecutionLease(
    Guid ProjectId,
    Guid SessionId,
    Guid FrameId,
    Guid TaskId,
    Guid ProjectAgentRoleId,
    string TargetRole,
    string TaskTitle,
    string DispatchSource);

/// <summary>
/// 任务完成后的续执行决策。
/// Follow-up execution decision produced after a task completes.
/// </summary>
/// <param name="NextLease">后续租约。 / Next lease to execute.</param>
/// <param name="SecretaryNotice">需要展示给秘书或用户的说明。 / Notice that should be surfaced by the secretary or UI.</param>
public sealed record ProjectGroupTaskCompletionResult(
    ProjectGroupExecutionLease? NextLease,
    string? SecretaryNotice);

/// <summary>
/// 手动恢复阻塞任务的结果。
/// Result returned when a blocked task is manually resumed.
/// </summary>
/// <param name="Lease">恢复后继续执行的租约。 / Lease to continue after the resume.</param>
public sealed record ProjectGroupTaskResumeResult(ProjectGroupExecutionLease Lease);

/// <summary>
/// ProjectGroup 能力申请。
/// Capability request raised inside a ProjectGroup execution.
/// </summary>
public sealed class ProjectGroupCapabilityRequest
{
    /// <summary>
    /// 申请的工具列表。
    /// Tools requested by the role.
    /// </summary>
    public IReadOnlyList<string> RequiredTools { get; init; } = [];

    /// <summary>
    /// 申请原因说明。
    /// Optional reason explaining why the capability is needed.
    /// </summary>
    public string? Reason { get; init; }
}

internal sealed class ProjectGroupDispatchEnvelope
{
    private const string OpenTag = "<openstaff_project_dispatch>";
    private const string CloseTag = "</openstaff_project_dispatch>";

    public IReadOnlyList<ProjectGroupDispatchInstruction> Dispatches { get; init; } = [];

    /// <summary>
    /// 从秘书回复中提取任务分发封装。
    /// Extracts the task dispatch envelope from a secretary response.
    /// </summary>
    public static bool TryExtract(
        string? content,
        out string displayContent,
        out ProjectGroupDispatchEnvelope envelope)
    {
        displayContent = content?.Trim() ?? string.Empty;
        envelope = new ProjectGroupDispatchEnvelope();

        if (string.IsNullOrWhiteSpace(content))
            return false;

        var start = content.IndexOf(OpenTag, StringComparison.Ordinal);
        var end = content.IndexOf(CloseTag, StringComparison.Ordinal);
        if (start < 0 || end <= start)
            return false;

        var json = content.Substring(start + OpenTag.Length, end - start - OpenTag.Length).Trim();
        var before = content[..start].Trim();
        var after = content[(end + CloseTag.Length)..].Trim();

        displayContent = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            new[] { before, after }.Where(part => !string.IsNullOrWhiteSpace(part)));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("dispatches", out var dispatchArray) || dispatchArray.ValueKind != JsonValueKind.Array)
            return false;

        var dispatches = new List<ProjectGroupDispatchInstruction>();
        foreach (var item in dispatchArray.EnumerateArray())
        {
            var targetRole = item.TryGetProperty("targetRole", out var target)
                ? target.GetString()
                : item.TryGetProperty("target", out var legacyTarget)
                    ? legacyTarget.GetString()
                    : null;

            var task = item.TryGetProperty("task", out var taskProperty)
                ? taskProperty.GetString()
                : item.TryGetProperty("message", out var legacyMessage)
                    ? legacyMessage.GetString()
                    : null;

            if (string.IsNullOrWhiteSpace(targetRole) || string.IsNullOrWhiteSpace(task))
                continue;

            dispatches.Add(new ProjectGroupDispatchInstruction
            {
                TargetRole = targetRole.Trim(),
                Task = task.Trim()
            });
        }

        envelope = new ProjectGroupDispatchEnvelope
        {
            Dispatches = dispatches
        };

        return true;
    }
}

/// <summary>
/// 秘书产出的单条分发指令。
/// Single dispatch instruction emitted by the secretary.
/// </summary>
public sealed class ProjectGroupDispatchInstruction
{
    /// <summary>
    /// 要分发到的目标角色。
    /// Target role for the dispatch.
    /// </summary>
    public string TargetRole { get; init; } = string.Empty;

    /// <summary>
    /// 要求目标角色完成的任务说明。
    /// Task description that the target role should execute.
    /// </summary>
    public string Task { get; init; } = string.Empty;
}

internal sealed class ProjectGroupCapabilityRequestEnvelope
{
    private const string OpenTag = "<openstaff_capability_request>";
    private const string CloseTag = "</openstaff_capability_request>";

    public ProjectGroupCapabilityRequest? Request { get; init; }

    /// <summary>
    /// 从角色回复中提取能力申请封装。
    /// Extracts the capability request envelope from a role response.
    /// </summary>
    public static bool TryExtract(
        string? content,
        out string displayContent,
        out ProjectGroupCapabilityRequestEnvelope envelope)
    {
        displayContent = content?.Trim() ?? string.Empty;
        envelope = new ProjectGroupCapabilityRequestEnvelope();

        if (string.IsNullOrWhiteSpace(content))
            return false;

        var start = content.IndexOf(OpenTag, StringComparison.Ordinal);
        var end = content.IndexOf(CloseTag, StringComparison.Ordinal);
        if (start < 0 || end <= start)
            return false;

        var json = content.Substring(start + OpenTag.Length, end - start - OpenTag.Length).Trim();
        var before = content[..start].Trim();
        var after = content[(end + CloseTag.Length)..].Trim();

        displayContent = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            new[] { before, after }.Where(part => !string.IsNullOrWhiteSpace(part)));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var tools = root.TryGetProperty("requiredTools", out var toolArray) && toolArray.ValueKind == JsonValueKind.Array
            ? toolArray.EnumerateArray()
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
                .ToList()
            : [];

        envelope = new ProjectGroupCapabilityRequestEnvelope
        {
            Request = new ProjectGroupCapabilityRequest
            {
                RequiredTools = tools,
                Reason = root.TryGetProperty("reason", out var reason) ? reason.GetString() : null
            }
        };

        return true;
    }
}

