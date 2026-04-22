using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.GitHub.Copilot;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent;
using OpenStaff.Agent.Services;
using OpenStaff.Agent.Vendor.GitHubCopilot;
using OpenStaff.Agents;
using OpenStaff.Dtos;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using DomainChatMessage = OpenStaff.Entities.ChatMessage;
using OpenStaff.Repositories;

namespace OpenStaff.Agent.Services.Adapters;

/// <summary>
/// zh-CN: 为应用默认运行时准备角色、供应商、消息历史与运行选项。
/// en: Prepares role, provider, message history, and run options for the default application-backed runtime.
/// </summary>
public sealed class ApplicationAgentRunFactory : IAgentRunFactory
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AgentFactory _agentFactory;
    private readonly IAgentPromptGenerator _agentPromptGenerator;
    private readonly IAgentMcpToolService _agentMcpToolService;
    private readonly IAgentSkillRuntimeService _agentSkillRuntimeService;
    private readonly IGitHubCopilotSessionManager _gitHubCopilotSessionManager;
    private readonly ILogger<ApplicationAgentRunFactory> _logger;

    /// <summary>
    /// zh-CN: 使用应用层依赖初始化默认运行时执行工厂。
    /// en: Initializes the default runtime execution factory with application-layer dependencies.
    /// </summary>
    public ApplicationAgentRunFactory(
        IServiceScopeFactory scopeFactory,
        AgentFactory agentFactory,
        IAgentPromptGenerator agentPromptGenerator,
        IAgentMcpToolService agentMcpToolService,
        IAgentSkillRuntimeService agentSkillRuntimeService,
        IGitHubCopilotSessionManager gitHubCopilotSessionManager,
        ILogger<ApplicationAgentRunFactory> logger)
    {
        _scopeFactory = scopeFactory;
        _agentFactory = agentFactory;
        _agentPromptGenerator = agentPromptGenerator;
        _agentMcpToolService = agentMcpToolService;
        _agentSkillRuntimeService = agentSkillRuntimeService;
        _gitHubCopilotSessionManager = gitHubCopilotSessionManager;
        _logger = logger;
    }

    /// <summary>
    /// zh-CN: 兼容旧测试构造签名；未显式提供 skill 运行时服务时，默认使用空实现。
    /// en: Preserves the legacy test constructor signature and falls back to a no-op skill runtime service when none is provided.
    /// </summary>
    public ApplicationAgentRunFactory(
        IServiceScopeFactory scopeFactory,
        AgentFactory agentFactory,
        IAgentPromptGenerator agentPromptGenerator,
        IAgentMcpToolService agentMcpToolService,
        IGitHubCopilotSessionManager gitHubCopilotSessionManager,
        ILogger<ApplicationAgentRunFactory> logger)
        : this(
            scopeFactory,
            agentFactory,
            agentPromptGenerator,
            agentMcpToolService,
            NoopAgentSkillRuntimeService.Instance,
            gitHubCopilotSessionManager,
            logger)
    {
    }

    /// <summary>
    /// zh-CN: 为逻辑消息解析本次执行所需的全部上下文。
    /// en: Resolves all execution inputs required for the logical message.
    /// </summary>
    public async Task<PreparedAgentRun> PrepareAsync(
        CreateMessageRequest request,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var agentRoles = serviceProvider.GetRequiredService<IAgentRoleRepository>();
        var projectAgentRoles = serviceProvider.GetRequiredService<IProjectAgentRoleRepository>();
        var projects = serviceProvider.GetRequiredService<IProjectRepository>();
        var chatMessages = serviceProvider.GetRequiredService<IChatMessageRepository>();

        // 这里才是测试对话真正进入运行时装配链的入口：
        // 1. 先根据 ProjectAgentRoleId / AgentRoleId / TargetRole 重新解析“谁来执行”；
        // 2. 再把前端传来的 OverrideJson 叠加到数据库角色上，生成最终执行画像。
        // 所以 API 层提前构造的内存角色快照，只用于展示和返回结果，不是最终执行对象。
        var resolvedExecution = await ResolveExecutionAsync(agentRoles, projectAgentRoles, projects, request, cancellationToken);
        var roleOverride = DeserializeOverride(request.OverrideJson);
        var effectiveRole = AgentRoleExecutionProfileFactory.CreateEffectiveRole(resolvedExecution.Role, roleOverride);

        ValidateRoleBindings(effectiveRole);
        var agentContext = BuildAgentContext(effectiveRole, resolvedExecution.Project, resolvedExecution.ProjectAgentRole, request);
        // Skill 和 MCP 是两条完全独立的链：
        // - Skill：先解析出真实技能目录，挂到 AgentContext，交给不同 Provider 当作上下文能力；
        // - MCP：稍后单独解析成 AITool，挂到 RunOptions.ChatOptions.Tools。
        var skillRuntimePayload = await _agentSkillRuntimeService.LoadRuntimePayloadAsync(
            new AgentSkillLoadContext(
                request.Scene,
                resolvedExecution.ProjectAgentRole?.Id,
                // 测试对话没有 ProjectAgentRole，Skill 解析依赖的是 AgentRoleId。
                request.Scene == MessageScene.Test ? resolvedExecution.Role.Id : null),
            cancellationToken);
        if (skillRuntimePayload is not null)
        {
            // Provider 在创建 Agent 时会从 AgentContext 里取这个 payload，
            // 再把技能目录包装成 AIContextProvider 或各自平台支持的 SkillDirectories。
            agentContext.SetSkillRuntimePayload(skillRuntimePayload);

            if (skillRuntimePayload.MissingBindings.Count > 0)
            {
                // Skill 缺失只记 warning，不会中断整个对话。
                // 这也是为什么前端可能表现为“没有生效”，但测试对话本身仍然能继续跑。
                _logger.LogWarning(
                    "Runtime skipped {MissingCount} missing skill bindings for role in scene {Scene}",
                    skillRuntimePayload.MissingBindings.Count,
                    request.Scene);
            }
        }

        var agent = await _agentFactory.CreateAgentAsync(effectiveRole, agentContext);
        try
        {
            var messages = await RestoreMessagesAsync(chatMessages, request, cancellationToken);
            var mcpTools = await _agentMcpToolService.LoadEnabledToolsAsync(
                new AgentMcpToolLoadContext(
                    request.Scene,
                    resolvedExecution.ProjectAgentRole?.Id,
                    // 测试对话的 MCP 工具同样走 AgentRoleId，而不是 ProjectAgentRoleId。
                    request.Scene == MessageScene.Test ? resolvedExecution.Role.Id : null,
                    request.MessageContext.SessionId,
                    TryGetDispatchSource(request.MessageContext.Extra)),
                cancellationToken)
                ?? [];
            // MCP 工具在这里才真正进入模型运行参数；
            // Skill payload 不会出现在这个集合里，所以永远不会变成 ToolCalls。
            var runOptions = BuildRunOptions(request.Scene, roleOverride, mcpTools);
            AgentSession? session = null;
            IAsyncDisposable? executionLease = null;

            var gitHubCopilotAgent = agent.GetService<GitHubCopilotAgent>();
            if (gitHubCopilotAgent is not null)
            {
                var preparedSession = await _gitHubCopilotSessionManager.PrepareSessionAsync(
                    gitHubCopilotAgent,
                    agentContext,
                    cancellationToken);
                session = preparedSession.Session;
                executionLease = preparedSession.ExecutionLease;
            }

            if (agent.GetService<AIAgent>() is not null)
                agent = agent.AsContextualStaffAgent(serviceProvider, messages, session, runOptions);

            _logger.LogDebug(
                "Prepared runtime execution for message {MessageId}, role scene {Scene}",
                messageId,
                request.Scene);

            return new PreparedAgentRun(
                Agent: agent,
                Messages: messages,
                Session: session,
                RunOptions: runOptions,
                ExecutionLease: executionLease,
                AgentRole: ResolveExecutionRole(effectiveRole),
                Model: ResolveModelName(effectiveRole, resolvedExecution.Project));
        }
        catch
        {
            await agent.DisposeAsync();

            throw;
        }
    }

    /// <summary>
    /// zh-CN: 构造传给提供程序的统一 <see cref="AgentContext" />，并为非项目场景补齐最小默认项目与语言信息。
    /// en: Builds the unified <see cref="AgentContext" /> passed to providers and fills in a minimal default project/language for non-project runs.
    /// </summary>
    private static AgentContext BuildAgentContext(
        AgentRole effectiveRole,
        Project? project,
        ProjectAgentRole? projectAgent,
        CreateMessageRequest request)
    {
        var extraConfig = request.MessageContext.Extra?
            .ToDictionary(item => item.Key, item => (object)item.Value)
            ?? new Dictionary<string, object>();

        return new AgentContext
        {
            ProjectId = project?.Id,
            SessionId = request.MessageContext.SessionId,
            ProjectAgentRoleId = projectAgent?.Id,
            Project = project ?? new Project { Language = "zh-CN" },
            Language = project?.Language ?? "zh-CN",
            Role = effectiveRole,
            Scene = ToSceneType(request.Scene),
            AgentInstanceId = projectAgent?.Id ?? effectiveRole.Id,
            ExtraConfig = extraConfig
        };
    }

    private static string? TryGetDispatchSource(IReadOnlyDictionary<string, string>? extra)
        => extra != null
            && extra.TryGetValue("openstaff_dispatch_source", out var value)
            && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    /// <summary>
    /// zh-CN: 按“项目成员 → 显式角色 → 目标角色字符串”的优先级解析本次执行要使用的角色与项目锚点。
    /// en: Resolves the role and project anchors for this run in priority order: project member, explicit role id, then target-role text.
    /// </summary>
    private async Task<ResolvedExecution> ResolveExecutionAsync(
        IAgentRoleRepository agentRoles,
        IProjectAgentRoleRepository projectAgentRoles,
        IProjectRepository projects,
        CreateMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MessageContext.ProjectAgentRoleId.HasValue)
        {
            var projectAgent = await projectAgentRoles
                .AsNoTracking()
                .Include(item => item.AgentRole)
                .FirstOrDefaultAsync(item => item.Id == request.MessageContext.ProjectAgentRoleId.Value, cancellationToken)
                ?? throw new InvalidOperationException($"Project agent '{request.MessageContext.ProjectAgentRoleId}' was not found.");

            if (projectAgent.AgentRole == null)
                throw new InvalidOperationException($"Project agent '{projectAgent.Id}' does not have a role.");

            if (request.MessageContext.ProjectId.HasValue && request.MessageContext.ProjectId != projectAgent.ProjectId)
            {
                throw new InvalidOperationException(
                    $"Project agent '{projectAgent.Id}' does not belong to project '{request.MessageContext.ProjectId}'.");
            }

            var resolvedProject = await LoadProjectAsync(projects, projectAgent.ProjectId, cancellationToken);
            return new ResolvedExecution(projectAgent.AgentRole, resolvedProject, projectAgent);
        }

        Project? project = null;
        if (request.MessageContext.ProjectId.HasValue)
            project = await LoadProjectAsync(projects, request.MessageContext.ProjectId.Value, cancellationToken);

        if (request.AgentRoleId.HasValue)
        {
            // 注意：这里只重新读取 AgentRole 实体本身，不会在这里 Include MCP/Skill 绑定。
            // 两种绑定都由专门的运行时服务在后面单独查询，这样职责更清晰。
            var role = await agentRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.AgentRoleId.Value && item.IsActive, cancellationToken)
                ?? throw new InvalidOperationException($"Agent role '{request.AgentRoleId}' was not found.");

            return new ResolvedExecution(role, project, null);
        }

        if (string.IsNullOrWhiteSpace(request.MessageContext.TargetRole))
            throw new InvalidOperationException("TargetRole is required when AgentRoleId and ProjectAgentRoleId are absent.");

        var targetRole = request.MessageContext.TargetRole.Trim();
        var normalizedTargetRole = targetRole.ToUpperInvariant();

        if (project?.Id is Guid projectId)
        {
            var projectAgents = await projectAgentRoles
                .AsNoTracking()
                .Include(item => item.AgentRole)
                .Where(item => item.ProjectId == projectId && item.AgentRole != null && item.AgentRole.IsActive)
                .ToListAsync(cancellationToken);

            var matchedProjectAgent = projectAgents.FirstOrDefault(item => item.AgentRole != null && MatchesRole(item.AgentRole, targetRole));
            if (matchedProjectAgent?.AgentRole != null)
                return new ResolvedExecution(matchedProjectAgent.AgentRole, project, matchedProjectAgent);
        }

        var normalizedTargetRoleKey = AgentJobTitleCatalog.NormalizeKey(request.MessageContext.TargetRole);
        var roleFromCatalog = (await agentRoles
                .AsNoTracking()
                .Where(item => item.IsActive)
                .ToListAsync(cancellationToken))
            .FirstOrDefault(item =>
                string.Equals(item.Name, request.MessageContext.TargetRole, StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    AgentJobTitleCatalog.NormalizeKey(item.JobTitle),
                    normalizedTargetRoleKey,
                    StringComparison.OrdinalIgnoreCase)
                || (item.IsBuiltin && AgentJobTitleCatalog.IsSecretary(request.MessageContext.TargetRole)))
            ?? throw new InvalidOperationException($"Target role '{request.MessageContext.TargetRole}' was not found.");

        return new ResolvedExecution(roleFromCatalog, project, null);
    }

    /// <summary>
    /// zh-CN: 用角色类型、显示名或职位对目标角色文本做不区分大小写匹配，兼容 @ 提及和系统分派。
    /// en: Matches target-role text against role type, display name, or job title case-insensitively so both @mentions and system dispatches resolve correctly.
    /// </summary>
    private static bool MatchesRole(AgentRole role, string targetRole)
    {
        return string.Equals(role.Name, targetRole, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                AgentJobTitleCatalog.NormalizeKey(role.JobTitle),
                AgentJobTitleCatalog.NormalizeKey(targetRole),
                StringComparison.OrdinalIgnoreCase)
            || (role.IsBuiltin && AgentJobTitleCatalog.IsSecretary(targetRole));
    }

    private static string ResolveExecutionRole(AgentRole role)
        => !string.IsNullOrWhiteSpace(role.JobTitle)
            ? AgentJobTitleCatalog.NormalizeKey(role.JobTitle) ?? role.JobTitle
            : role.IsBuiltin
                ? BuiltinRoleTypes.Secretary
                : role.Name;

    /// <summary>
    /// zh-CN: 解析单次消息的角色覆盖 JSON，并在格式非法时立即抛错而不是静默忽略用户输入。
    /// en: Parses per-message role override JSON and fails fast on malformed payloads instead of silently ignoring user input.
    /// </summary>
    private static AgentRoleInput? DeserializeOverride(string? overrideJson)
    {
        if (string.IsNullOrWhiteSpace(overrideJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<AgentRoleInput>(overrideJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("OverrideJson is invalid.", nameof(overrideJson), ex);
        }
    }

    private static void ValidateRoleBindings(AgentRole role)
    {
        if (!role.ModelProviderId.HasValue)
            throw new InvalidOperationException($"Role '{role.Name}' must bind a provider account.");

        if (string.IsNullOrWhiteSpace(role.ModelName))
            throw new InvalidOperationException($"Role '{role.Name}' must bind a model.");
    }

    /// <summary>
    /// zh-CN: 按场景、MCP 工具和临时覆盖拼装本次运行选项，没有任何覆盖时返回 <see langword="null" /> 以复用默认执行路径。
    /// en: Builds per-run options from scene rules, MCP tools, and transient overrides, returning <see langword="null" /> when the default execution path can be reused unchanged.
    /// </summary>
    private static AgentRunOptions? BuildRunOptions(
        MessageScene scene,
        AgentRoleInput? roleOverride,
        IReadOnlyList<AITool>? mcpTools)
    {
        var chatOptions = new ChatOptions();
        var hasOptions = false;

        if (scene == MessageScene.Test)
        {
            // 测试场景默认打开 reasoning summary，方便在前端排查模型为什么做出某个决定。
            chatOptions.Reasoning = new() { Output = ReasoningOutput.Summary, Effort = ReasoningEffort.Medium };
            hasOptions = true;
        }

        if (mcpTools is { Count: > 0 })
        {
            // MCP 是“真工具”，最终会进入模型的 Tools 列表，因此后面有机会出现在 toolCalls 里。
            chatOptions.Tools = [.. mcpTools];
            hasOptions = true;
        }

        return hasOptions ? new ChatClientAgentRunOptions(chatOptions) : null;
    }

    /// <summary>
    /// zh-CN: 恢复与当前消息直接相关的历史祖先链，并在末尾追加本次输入，避免把整场会话全部塞进模型上下文。
    /// en: Restores only the ancestor history directly relevant to the current message, then appends the current input so the whole session is not stuffed into model context.
    /// </summary>
    private async Task<IReadOnlyList<AIChatMessage>> RestoreMessagesAsync(
        IChatMessageRepository chatMessages,
        CreateMessageRequest request,
        CancellationToken cancellationToken)
    {
        var messages = new List<AIChatMessage>();

        if (request.MessageContext.SessionId.HasValue && request.MessageContext.ParentMessageId.HasValue)
        {
            var history = await chatMessages
                .AsNoTracking()
                .Where(item => item.SessionId == request.MessageContext.SessionId.Value)
                .ToListAsync(cancellationToken);

            var historyById = history.ToDictionary(item => item.Id);
            // zh-CN: 仅恢复从 ParentMessageId 向上的祖先链，避免把整个会话都塞给模型。
            // en: Restore only the ancestor lineage from ParentMessageId instead of loading the entire session into the model context.
            foreach (var item in BuildMessageLineage(historyById, request.MessageContext.ParentMessageId.Value))
                messages.Add(ToAiMessage(item));
        }

        messages.Add(new AIChatMessage(request.InputRole, request.Input));
        return messages;
    }

    /// <summary>
    /// zh-CN: 从叶子消息沿父链回溯并按时间顺序输出历史，用于最小化上下文恢复。
    /// en: Walks parent links from the leaf message and returns the lineage in chronological order for minimal context restoration.
    /// </summary>
    private static IReadOnlyList<DomainChatMessage> BuildMessageLineage(
        IReadOnlyDictionary<Guid, DomainChatMessage> historyById,
        Guid leafMessageId)
    {
        var lineage = new Stack<DomainChatMessage>();
        var currentId = leafMessageId;

        while (historyById.TryGetValue(currentId, out var message))
        {
            lineage.Push(message);

            if (message.ParentMessageId == null)
                break;

            currentId = message.ParentMessageId.Value;
        }

        return [.. lineage];
    }

    /// <summary>
    /// zh-CN: 将持久化聊天消息转换成 AI 运行时可消费的消息对象。
    /// en: Converts a persisted chat message into the AI runtime message representation.
    /// </summary>
    private static AIChatMessage ToAiMessage(DomainChatMessage message)
    {
        return new AIChatMessage(ToChatRole(message.Role), message.Content);
    }

    /// <summary>
    /// zh-CN: 把数据库中的角色字符串映射到聊天运行时角色，未知值默认回退为用户消息。
    /// en: Maps stored role strings to runtime chat roles and falls back to user messages for unknown values.
    /// </summary>
    private static ChatRole ToChatRole(string role)
    {
        return role switch
        {
            MessageRoles.Assistant => ChatRole.Assistant,
            MessageRoles.System => ChatRole.System,
            MessageRoles.Tool => ChatRole.Tool,
            _ => ChatRole.User
        };
    }

    /// <summary>
    /// zh-CN: 将服务层消息场景映射为核心场景枚举，未识别值返回空以保留调用方兜底空间。
    /// en: Maps service-layer message scenes to the core scene enum and returns null for unrecognized values so callers retain a fallback path.
    /// </summary>
    private static SceneType? ToSceneType(MessageScene scene)
    {
        return scene switch
        {
            MessageScene.Test => SceneType.Test,
            MessageScene.Private => SceneType.Private,
            MessageScene.TeamGroup => SceneType.TeamGroup,
            MessageScene.ProjectBrainstorm => SceneType.ProjectBrainstorm,
            MessageScene.ProjectGroup => SceneType.ProjectGroup,
            _ => null
        };
    }

    /// <summary>
    /// zh-CN: 计算用于日志和回放摘要的有效模型名，优先使用角色自身配置，再回退到项目默认值。
    /// en: Computes the effective model name used for logs and summaries, preferring the role's own configuration before the project default.
    /// </summary>
    private static string? ResolveModelName(AgentRole role, Project? project)
        => role.ModelName;

    /// <summary>
    /// zh-CN: 加载项目及其已分配智能体图谱，因为角色解析、场景提示和分派都依赖这些关联数据。
    /// en: Loads the project together with its assigned-agent graph because role resolution, scene prompts, and dispatch logic depend on those relationships.
    /// </summary>
    private static async Task<Project> LoadProjectAsync(
        IProjectRepository projects,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return await projects
            .AsNoTracking()
            .Include(project => project.AgentRoles)
                .ThenInclude(projectAgent => projectAgent.AgentRole)
            .FirstOrDefaultAsync(project => project.Id == projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project '{projectId}' was not found.");
    }

    /// <summary>
    /// zh-CN: 封装一次解析出的执行主体及其可选项目锚点，供后续 provider、prompt 和 telemetry 统一复用。
    /// en: Bundles the resolved execution role together with optional project anchors so provider resolution, prompt generation, and telemetry can reuse them consistently.
    /// </summary>
    private sealed record ResolvedExecution(
        AgentRole Role,
        Project? Project,
        ProjectAgentRole? ProjectAgentRole);

    private sealed class NoopAgentSkillRuntimeService : IAgentSkillRuntimeService
    {
        public static readonly NoopAgentSkillRuntimeService Instance = new();

        public Task<AgentSkillRuntimePayload?> LoadRuntimePayloadAsync(AgentSkillLoadContext context, CancellationToken ct)
            => Task.FromResult<AgentSkillRuntimePayload?>(null);
    }
}
