using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Application.Conversations.Models;
using OpenStaff.Application.Conversations.Services;
using OpenStaff.Application.Sessions.Services;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.Infrastructure.Export;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Projects.Services;
/// <summary>
/// 项目核心服务，负责项目生命周期、工作区初始化和脑暴状态落盘。
/// Core project service that manages project lifecycle, workspace initialization, and brainstorm-state persistence.
/// </summary>
public class ProjectService
{
    private readonly IProjectRepository _projects;
    private readonly IProjectAgentRoleRepository _projectAgents;
    private readonly IAgentRoleRepository _agentRoles;
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskDependencyRepository _taskDependencies;
    private readonly IAgentEventRepository _agentEvents;
    private readonly ICheckpointRepository _checkpoints;
    private readonly IChatSessionRepository _chatSessions;
    private readonly IChatFrameRepository _chatFrames;
    private readonly IChatMessageRepository _chatMessages;
    private readonly ISessionEventRepository _sessionEvents;
    private readonly IRepositoryContext _repositoryContext;
    private readonly ProjectExporter _exporter;
    private readonly ProjectImporter _importer;
    private readonly IConfiguration _config;
    private readonly ConversationTriggerService _conversationTriggerService;
    private readonly RoleCapabilityBindingService _roleCapabilityBindingService;
    private readonly McpHub? _mcpHub;
    private readonly McpWarmupCoordinator? _mcpWarmupCoordinator;
    private readonly SessionRunner? _sessionRunner;
    private readonly ILogger<ProjectService> _logger;
    private readonly IOptions<OpenStaffOptions> _openStaffOptions;

    /// <summary>
    /// 初始化项目核心服务。
    /// Initializes the core project service.
    /// </summary>
    public ProjectService(
        IProjectRepository projects,
        IProjectAgentRoleRepository projectAgents,
        IAgentRoleRepository agentRoles,
        ITaskItemRepository tasks,
        ITaskDependencyRepository taskDependencies,
        IAgentEventRepository agentEvents,
        ICheckpointRepository checkpoints,
        IChatSessionRepository chatSessions,
        IChatFrameRepository chatFrames,
        IChatMessageRepository chatMessages,
        ISessionEventRepository sessionEvents,
        IRepositoryContext repositoryContext,
        ProjectExporter exporter,
        ProjectImporter importer,
        IConfiguration config,
        ConversationTriggerService conversationTriggerService,
        RoleCapabilityBindingService roleCapabilityBindingService,
        ILogger<ProjectService> logger,
        IOptions<OpenStaffOptions> openStaffOptions,
        SessionRunner? sessionRunner = null,
        McpHub? mcpHub = null,
        McpWarmupCoordinator? mcpWarmupCoordinator = null)
    {
        _projects = projects;
        _projectAgents = projectAgents;
        _agentRoles = agentRoles;
        _tasks = tasks;
        _taskDependencies = taskDependencies;
        _agentEvents = agentEvents;
        _checkpoints = checkpoints;
        _chatSessions = chatSessions;
        _chatFrames = chatFrames;
        _chatMessages = chatMessages;
        _sessionEvents = sessionEvents;
        _repositoryContext = repositoryContext;
        _exporter = exporter;
        _importer = importer;
        _config = config;
        _conversationTriggerService = conversationTriggerService;
        _roleCapabilityBindingService = roleCapabilityBindingService;
        _mcpHub = mcpHub;
        _mcpWarmupCoordinator = mcpWarmupCoordinator;
        _sessionRunner = sessionRunner;
        _logger = logger;
        _openStaffOptions = openStaffOptions;
    }

    /// <summary>
    /// 获取所有项目。
    /// Gets all projects.
    /// </summary>
    public async Task<List<Project>> GetAllAsync(CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogDebug("Fetching all projects");

            var projects = await _projects
                .OrderByDescending(p => p.UpdatedAt)
                .Include(p => p.AgentRoles)
                .ToListAsync(ct);

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("Retrieved {Count} projects in {ElapsedMs}ms", projects.Count, elapsed);

            return projects;
        }
        catch (Exception ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Error fetching all projects after {ElapsedMs}ms", elapsed);
            throw;
        }
    }

    /// <summary>
    /// 根据标识获取项目详情。
    /// Gets project details by identifier.
    /// </summary>
    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogDebug("Fetching project {ProjectId}", id);

            var project = await _projects
                .Include(p => p.AgentRoles)
                .ThenInclude(a => a.AgentRole)
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            if (project != null)
            {
                _logger.LogInformation("Retrieved project {ProjectId} in {ElapsedMs}ms", id, elapsed);
            }
            else
            {
                _logger.LogWarning("Project {ProjectId} not found", id);
            }

            return project;
        }
        catch (Exception ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Error fetching project {ProjectId} after {ElapsedMs}ms", id, elapsed);
            throw;
        }
    }

    /// <summary>
    /// 创建项目。
    /// Creates a project.
    /// </summary>
    public async Task<Project> CreateAsync(CreateProjectRequest request, CancellationToken ct)
    {
        var project = new Project
        {
            Name = request.Name,
            Description = request.Description,
            Language = request.Language ?? "zh-CN",
            Phase = ProjectPhases.Brainstorming,
            DefaultProviderId = request.DefaultProviderId,
            DefaultModelName = request.DefaultModelName,
        };

        _projects.Add(project);
        await _repositoryContext.SaveChangesAsync(ct);
        return project;
    }

    /// <summary>
    /// 更新项目设置。
    /// Updates project settings.
    /// </summary>
    public async Task<Project?> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await _projects.FindAsync(id, ct);
        if (project == null) return null;

        if (request.Name != null) project.Name = request.Name;
        if (request.Description != null) project.Description = request.Description;
        if (request.Language != null) project.Language = request.Language;
        if (request.DefaultProviderId.HasValue) project.DefaultProviderId = request.DefaultProviderId;
        if (request.DefaultModelName != null) project.DefaultModelName = request.DefaultModelName;
        if (request.ExtraConfig != null) project.ExtraConfig = request.ExtraConfig;
        project.UpdatedAt = DateTime.UtcNow;

        await _repositoryContext.SaveChangesAsync(ct);
        return project;
    }

    /// <summary>
    /// 删除项目及其关联图谱数据。
    /// Deletes a project together with its related graph data.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var project = await _projects
            .AsNoTracking()
            .FirstOrDefaultAsync(project => project.Id == id, ct);
        if (project == null) return false;

        await DeleteSessionGraphAsync(id, ct);
        await _checkpoints.Where(checkpoint => checkpoint.ProjectId == id).ExecuteDeleteAsync(ct);
        await DeleteTaskGraphAsync(id, ct);
        await DeleteAgentEventGraphAsync(id, ct);
        await _projectAgents.Where(agent => agent.ProjectId == id).ExecuteDeleteAsync(ct);

        await _projects
            .Where(project => project.Id == id)
            .ExecuteDeleteAsync(ct);

        if (!string.IsNullOrWhiteSpace(project.WorkspacePath))
        {
            var mcpDirectory = Path.Combine(project.WorkspacePath, ".mcp");
            if (Directory.Exists(mcpDirectory))
                Directory.Delete(mcpDirectory, recursive: true);
        }

        _mcpWarmupCoordinator?.ForgetProject(id);

        return true;
    }

    /// <summary>
    /// 初始化项目工作区、脑暴文档和 Git 仓库。
    /// Initializes the project workspace, brainstorm document, and Git repository.
    /// </summary>
    public async Task InitializeAsync(Guid id, CancellationToken ct)
    {
        var project = await _projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project == null)
            throw new InvalidOperationException($"Project {id} not found");

        if (project.Status != ProjectStatus.Initializing && project.Status != ProjectStatus.Paused)
            throw new InvalidOperationException($"Project {id} is in {project.Status} status, cannot initialize");

        // zh-CN: 工作区路径一旦确定，就会成为 README、导入导出和后续执行的统一根目录。
        // en: Once the workspace path is determined, it becomes the shared root for README, import/export, and later execution flows.
        var workspacesRoot = _config["Workspaces:RootPath"]
            ?? Path.Combine(_openStaffOptions.Value.WorkingDirectory, "workspaces");
        var workspacePath = Path.Combine(workspacesRoot, project.Id.ToString("N"));
        Directory.CreateDirectory(workspacePath);
        project.WorkspacePath = workspacePath;

        var brainstormDirectory = Path.Combine(workspacePath, ".staff");
        Directory.CreateDirectory(brainstormDirectory);

        // zh-CN: 初始脑暴文档作为 ProjectBrainstorm 的落盘目标，确保秘书有明确的文档承载位置。
        // en: The initial brainstorm document is the persistence target for ProjectBrainstorm so the secretary always has a concrete document sink.
        var brainstormDocumentPath = Path.Combine(brainstormDirectory, "project-brainstorm.md");
        if (!File.Exists(brainstormDocumentPath))
        {
            await File.WriteAllTextAsync(
                brainstormDocumentPath,
                $"# {project.Name} 项目头脑风暴\n\n> 与秘书对话，持续完善这份文档，直到需求足够清晰可以推进下一步。\n",
                ct);
        }

        // zh-CN: Git 初始化失败不会阻塞项目启动，但会记录日志，便于后续补救。
        // en: Git initialization failures do not block project startup, but they are logged so the setup can be repaired later.
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", "init")
            {
                WorkingDirectory = workspacePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync(ct);
                _logger.LogInformation("Git initialized in {Path}", workspacePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Git init failed for project {ProjectId}, continuing without git", id);
        }

        // zh-CN: 初始化完成后把项目推进到可进行脑暴的活跃状态。
        // en: Once initialization succeeds, move the project into an active state that is ready for brainstorming.
        project.Status = ProjectStatus.Active;
        project.Phase = ProjectPhases.Brainstorming;
        project.UpdatedAt = DateTime.UtcNow;
        await _repositoryContext.SaveChangesAsync(ct);

        var kickoffSummary = BuildBrainstormKickoffSummary(project);
        await _conversationTriggerService.TriggerProjectSceneMessageAsync(
            new ProjectConversationTriggerEntry(
                project.Id,
                SessionSceneTypes.ProjectBrainstorm,
                kickoffSummary,
                kickoffSummary,
                BuildBrainstormKickoffMessage(project),
                AuthorRole: BuiltinRoleTypes.Secretary),
            ct);

        _logger.LogInformation("Project {ProjectId} ({Name}) initialized: workspace={Path}, phase={Phase}",
            id, project.Name, workspacePath, project.Phase);
    }

    private static string BuildBrainstormKickoffSummary(Project project)
    {
        return project.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? "项目初始化后已自动开启头脑风暴。"
            : "Project initialization automatically started brainstorming.";
    }

    private static string BuildBrainstormKickoffMessage(Project project)
    {
        return project.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? $"你好，我是这个项目的秘书。头脑风暴已经开始了。请先告诉我：你想做什么、面向谁、最想优先解决什么问题，以及目前有哪些明确约束。我会帮你整理成可推进的需求文档。"
            : $"Hi, I'm the project secretary. Brainstorming has started. Tell me what you want to build, who it is for, what problem should be solved first, and any constraints you already know. I'll turn that into a requirement document we can keep refining.";
    }

    private async Task EnsureProjectGroupKickoffAsync(Project project, Guid sessionId, CancellationToken ct)
    {
        var projectAgents = await _projectAgents
            .AsNoTracking()
            .Include(item => item.AgentRole)
            .Where(item => item.ProjectId == project.Id && item.AgentRole != null && item.AgentRole.IsActive)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(ct);

        var kickoffTargets = projectAgents
            .Where(item => item.AgentRole != null && !IsSecretaryRole(item.AgentRole))
            .ToList();

        var kickoffMessage = BuildProjectGroupKickoffMessage(project, kickoffTargets);
        var trigger = await _conversationTriggerService.TriggerProjectSceneMessageAsync(
            new ProjectConversationTriggerEntry(
                project.Id,
                SessionSceneTypes.ProjectGroup,
                BuildProjectGroupKickoffSummary(project),
                BuildProjectGroupKickoffFramePurpose(project),
                kickoffMessage,
                AuthorRole: BuiltinRoleTypes.Secretary,
                ContextStrategy: ContextStrategies.Hybrid,
                IdempotencyMode: ConversationTriggerIdempotencyModes.SkipIfSceneHasMessages),
            ct);

        if (trigger.Skipped || _sessionRunner == null || kickoffTargets.Count == 0 || trigger.FrameId is null)
            return;

        var dispatchTargets = kickoffTargets
            .Select(item => new ProjectGroupDispatchTarget(
                GetTargetRole(item.AgentRole!),
                BuildProjectGroupKickoffTask(item),
                BuildProjectGroupKickoffTask(item),
                false,
                item.Id,
                ResolveProjectAgentDisplayName(item),
                DispatchSource: "project_group_system_kickoff"))
            .ToList();

        await _sessionRunner.ExecuteProjectGroupDispatchesAsync(
            trigger.SessionId,
            trigger.FrameId.Value,
            trigger.MessageId,
            dispatchTargets,
            ct);
    }

    private static string BuildProjectGroupKickoffSummary(Project project)
    {
        return project.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? "项目已进入执行阶段，秘书已在项目群同步分工并点名首批成员回应。"
            : "The project has entered execution and the secretary has posted the kickoff assignment plan in the group chat.";
    }

    private static string BuildProjectGroupKickoffFramePurpose(Project project)
    {
        return project.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? "同步项目执行分工，并点名首批成员在群里确认开工。"
            : "Share the execution plan and ask the first project members to confirm kickoff in the group chat.";
    }

    private static string BuildProjectGroupKickoffMessage(Project project, IReadOnlyList<ProjectAgentRole> kickoffTargets)
    {
        if (project.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            var lines = new List<string>
            {
                $"大家好，{project.Name} 现在正式进入执行阶段。",
                "我先同步一下当前的项目分工："
            };

            lines.AddRange(kickoffTargets.Select(agent =>
                $"- @{ResolveProjectAgentDisplayName(agent)}：{ResolveKickoffResponsibility(agent)}"));

            if (kickoffTargets.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add($"请 {string.Join("、", kickoffTargets.Select(agent => $"@{ResolveProjectAgentDisplayName(agent)}"))} 先各自用一句话确认职责和起手动作。");
            }

            return string.Join(Environment.NewLine, lines);
        }

        var englishLines = new List<string>
        {
            $"{project.Name} is now in execution mode.",
            "Here is the current team split:"
        };
        englishLines.AddRange(kickoffTargets.Select(agent =>
            $"- @{ResolveProjectAgentDisplayName(agent)}: {ResolveKickoffResponsibility(agent)}"));
        if (kickoffTargets.Count > 0)
        {
            englishLines.Add(string.Empty);
            englishLines.Add($"Please {string.Join(", ", kickoffTargets.Select(agent => $"@{ResolveProjectAgentDisplayName(agent)}"))} each reply with a short hello, your responsibility, and what you will start first.");
        }

        return string.Join(Environment.NewLine, englishLines);
    }

    private static string BuildProjectGroupKickoffTask(ProjectAgentRole agent)
    {
        return $"请先在项目群里用一句简短消息打个招呼，说明你负责 {ResolveKickoffResponsibility(agent)}，并确认你现在准备先做什么。回复保持自然、简洁，像真实群聊。";
    }

    private static string ResolveKickoffResponsibility(ProjectAgentRole agent)
    {
        var currentTask = agent.CurrentTask?.Trim();
        if (!string.IsNullOrWhiteSpace(currentTask))
        {
            return currentTask;
        }

        var description = agent.AgentRole?.Description?.Trim();
        if (!string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        return !string.IsNullOrWhiteSpace(agent.AgentRole?.JobTitle)
            ? AgentJobTitleCatalog.NormalizeKey(agent.AgentRole!.JobTitle) ?? agent.AgentRole.JobTitle!
            : agent.AgentRole?.Name ?? "当前分工";
    }

    private static string ResolveProjectAgentDisplayName(ProjectAgentRole agent)
    {
        return !string.IsNullOrWhiteSpace(agent.AgentRole?.Name)
            ? agent.AgentRole!.Name
            : AgentJobTitleCatalog.NormalizeKey(agent.AgentRole?.JobTitle) ?? agent.AgentRole?.JobTitle ?? "Agent";
    }

    private static string GetTargetRole(AgentRole role)
        => !string.IsNullOrWhiteSpace(role.JobTitle)
            ? AgentJobTitleCatalog.NormalizeKey(role.JobTitle) ?? role.JobTitle
            : role.Name;

    private static bool IsSecretaryRole(AgentRole role)
    {
        return AgentJobTitleCatalog.IsSecretary(role.JobTitle)
            || AgentJobTitleCatalog.IsSecretary(role.Name);
    }

    /// <summary>
    /// 启动项目执行阶段。
    /// Starts the execution phase of a project.
    /// </summary>
    public async Task<Project> StartAsync(Guid id, CancellationToken ct)
    {
        var project = await _projects.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new InvalidOperationException($"Project {id} not found");

        if (project.Phase == ProjectPhases.Completed)
            throw new InvalidOperationException($"Project {id} is already completed");

        if (project.Phase != ProjectPhases.ReadyToStart && project.Phase != ProjectPhases.Running)
            throw new InvalidOperationException($"Project {id} is in phase {project.Phase}, cannot start");

        var now = DateTime.UtcNow;
        var activeBrainstormSessions = await _chatSessions
            .Where(s => s.ProjectId == id
                && s.Scene == SessionSceneTypes.ProjectBrainstorm
                && (s.Status == SessionStatus.Active || s.Status == SessionStatus.AwaitingInput))
            .ToListAsync(ct);

        foreach (var session in activeBrainstormSessions)
        {
            session.Status = SessionStatus.Completed;
            session.CompletedAt = now;
            session.FinalResult = "ProjectBrainstorm 已结束，项目已进入执行阶段。";
        }

        var activeGroupSession = await _chatSessions
            .Where(s => s.ProjectId == id
                && s.Scene == SessionSceneTypes.ProjectGroup
                && (s.Status == SessionStatus.Active || s.Status == SessionStatus.AwaitingInput))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (activeGroupSession == null)
        {
            activeGroupSession = new ChatSession
            {
                ProjectId = id,
                Scene = SessionSceneTypes.ProjectGroup,
                Status = SessionStatus.Active,
                InitialInput = "项目已启动，项目群聊已创建。",
                ContextStrategy = ContextStrategies.Hybrid
            };
            _chatSessions.Add(activeGroupSession);
        }

        project.Status = ProjectStatus.Active;
        project.Phase = ProjectPhases.Running;
        project.UpdatedAt = now;

        await _repositoryContext.SaveChangesAsync(ct);
        await EnsureProjectGroupKickoffAsync(project, activeGroupSession.Id, ct);
        _logger.LogInformation("Project {ProjectId} started, brainstorm sessions closed: {Count}", id, activeBrainstormSessions.Count);
        return project;
    }

    /// <summary>
    /// 将 ProjectBrainstorm 返回的结构化状态应用到项目文档和阶段。
    /// Applies the structured ProjectBrainstorm state to the project document and phase.
    /// </summary>
    public async Task<ProjectBrainstormApplyResult> ApplyBrainstormStateAsync(Guid projectId, string responseContent, CancellationToken ct)
    {
        var project = await _projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new InvalidOperationException($"Project {projectId} not found");

        if (!ProjectBrainstormStateEnvelope.TryExtract(responseContent, out var displayContent, out var envelope))
        {
            _logger.LogWarning("ProjectBrainstorm response for project {ProjectId} did not include state envelope", projectId);
            return new ProjectBrainstormApplyResult(displayContent, DocumentUpdated: false, PhaseChanged: false, CurrentPhase: project.Phase);
        }

        if (string.IsNullOrWhiteSpace(project.WorkspacePath))
            throw new InvalidOperationException($"Project {projectId} workspace is not initialized");

        var readmePath = GetBrainstormDocumentPath(project);
        Directory.CreateDirectory(project.WorkspacePath);
        Directory.CreateDirectory(Path.GetDirectoryName(readmePath)!);

        var normalizedDocument = NormalizeDocumentMarkdown(envelope.DocumentMarkdown);
        var existingDocument = File.Exists(readmePath)
            ? await File.ReadAllTextAsync(readmePath, ct)
            : string.Empty;

        var documentUpdated = !string.Equals(existingDocument, normalizedDocument, StringComparison.Ordinal);
        if (documentUpdated)
        {
            await File.WriteAllTextAsync(readmePath, normalizedDocument, ct);
        }

        var nextPhase = NormalizeBrainstormPhase(envelope.Phase) ?? project.Phase;
        var phaseChanged = !string.Equals(project.Phase, nextPhase, StringComparison.Ordinal);
        if (phaseChanged)
        {
            project.Phase = nextPhase;
        }

        if (documentUpdated || phaseChanged)
        {
            project.UpdatedAt = DateTime.UtcNow;
            await _repositoryContext.SaveChangesAsync(ct);
        }

        return new ProjectBrainstormApplyResult(displayContent, documentUpdated, phaseChanged, project.Phase);
    }

    /// <summary>
    /// 导出项目到归档文件。
    /// Exports a project to an archive file.
    /// </summary>
    public async Task<string> ExportAsync(Guid id, CancellationToken ct)
    {
        var exportBasePath = _config["FileStorage:ExportPath"];
        var exportPath = string.IsNullOrEmpty(exportBasePath)
            ? Path.Combine(Path.GetTempPath(), "openstaff-exports")
            : Path.IsPathRooted(exportBasePath)
                ? exportBasePath
                : Path.Combine(Directory.GetCurrentDirectory(), exportBasePath);

        Directory.CreateDirectory(exportPath);
        _logger.LogInformation("Exporting project {ProjectId} to {ExportPath}", id, exportPath);
        return await _exporter.ExportAsync(id, exportPath, ct);
    }

    /// <summary>
    /// 从归档文件导入项目。
    /// Imports a project from an archive file.
    /// </summary>
    public async Task<Guid> ImportAsync(IFormFile file, CancellationToken ct)
    {
        var tempBasePath = _config["FileStorage:TempPath"];
        var tempDir = string.IsNullOrEmpty(tempBasePath)
            ? Path.GetTempPath()
            : Path.IsPathRooted(tempBasePath)
                ? tempBasePath
                : Path.Combine(Directory.GetCurrentDirectory(), tempBasePath);

        Directory.CreateDirectory(tempDir);

        var tempFile = Path.Combine(tempDir, $"import-{Guid.NewGuid()}.openstaff");
        _logger.LogInformation("Importing project from {FileName} to {TempFile}", file.FileName, tempFile);

        using (var stream = new FileStream(tempFile, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }

        var workspacesRoot = _config["Workspaces:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "workspaces");
        Directory.CreateDirectory(workspacesRoot);

        return await _importer.ImportAsync(tempFile, workspacesRoot, ct);
    }

    /// <summary>
    /// 获取项目的员工列表（含角色详情）。
    /// Gets the project members together with their role details.
    /// </summary>
    public async Task<List<ProjectAgentRole>> GetProjectAgentsAsync(Guid projectId, CancellationToken ct)
    {
        return await _projectAgents
            .Where(pa => pa.ProjectId == projectId)
            .Include(pa => pa.AgentRole)
            .OrderBy(pa => pa.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>
    /// 批量设置项目参与的员工（全量替换）。
    /// Replaces the project member list in bulk.
    /// </summary>
    public async Task SetProjectAgentsAsync(Guid projectId, List<Guid> agentRoleIds, CancellationToken ct)
    {
        var project = await _projects.FindAsync(projectId, ct);
        if (project == null) throw new InvalidOperationException($"Project {projectId} not found");

        var secretaryRoleId = await _agentRoles
            .AsNoTracking()
            .Where(role => role.IsActive && role.IsBuiltin)
            .Select(role => role.Id)
            .FirstOrDefaultAsync(ct);
        if (secretaryRoleId == Guid.Empty)
            throw new InvalidOperationException("Secretary role is not available");

        var normalizedRoleIds = agentRoleIds.Distinct().ToList();
        if (!normalizedRoleIds.Contains(secretaryRoleId))
            normalizedRoleIds.Insert(0, secretaryRoleId);

        // zh-CN: 项目成员列表采用全量替换模式，先清空旧映射再写入新的角色集合。
        // en: Project membership uses a full-replacement model, so clear the old mappings before inserting the new role set.
        var existing = await _projectAgents.Where(pa => pa.ProjectId == projectId).ToListAsync(ct);
        _projectAgents.RemoveRange(existing);

        // zh-CN: Secretary 角色被强制保留，以保证项目群组始终拥有默认协调入口。
        // en: The secretary role is always preserved so ProjectGroup conversations keep a default coordination entrypoint.
        var createdProjectAgents = new List<ProjectAgentRole>(normalizedRoleIds.Count);
        foreach (var roleId in normalizedRoleIds)
        {
            var projectAgent = new ProjectAgentRole
            {
                ProjectId = projectId,
                AgentRoleId = roleId
            };
            createdProjectAgents.Add(projectAgent);
            _projectAgents.Add(projectAgent);
        }

        await _roleCapabilityBindingService.CopyRoleBindingsToProjectAgentsAsync(createdProjectAgents, ct, saveChanges: false);
        project.UpdatedAt = DateTime.UtcNow;
        await _repositoryContext.SaveChangesAsync(ct);
        if (_mcpHub != null)
            await _mcpHub.InvalidateProjectAsync(projectId);
        if (_mcpWarmupCoordinator != null)
            await _mcpWarmupCoordinator.RebuildProjectAsync(projectId, ct);
        _logger.LogInformation("Project {ProjectId} agents updated: {Count} roles", projectId, normalizedRoleIds.Count);
    }

    /// <summary>
    /// 会话消息、Frame 和事件都带有父子自引用，删除项目时需要先打断引用，再按图谱顺序清理。
    /// </summary>
    private async Task DeleteSessionGraphAsync(Guid projectId, CancellationToken ct)
    {
        var sessionIds = await _chatSessions
            .Where(session => session.ProjectId == projectId)
            .Select(session => session.Id)
            .ToListAsync(ct);
        if (sessionIds.Count == 0)
            return;

        await _sessionEvents
            .Where(sessionEvent => sessionIds.Contains(sessionEvent.SessionId))
            .ExecuteDeleteAsync(ct);

        await _chatMessages
            .Where(message => sessionIds.Contains(message.SessionId) && message.ParentMessageId != null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(message => message.ParentMessageId, message => (Guid?)null),
                ct);

        await _chatMessages
            .Where(message => sessionIds.Contains(message.SessionId))
            .ExecuteDeleteAsync(ct);

        await _chatFrames
            .Where(frame => sessionIds.Contains(frame.SessionId) && frame.ParentFrameId != null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(frame => frame.ParentFrameId, frame => (Guid?)null),
                ct);

        await _chatFrames
            .Where(frame => sessionIds.Contains(frame.SessionId))
            .ExecuteDeleteAsync(ct);

        await _chatSessions
            .Where(session => sessionIds.Contains(session.Id))
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// 删除项目任务图谱及其依赖关系。
    /// Deletes the project's task graph together with its dependency links.
    /// </summary>
    /// <param name="projectId">项目标识。/ Project identifier.</param>
    /// <param name="ct">取消令牌。/ Cancellation token.</param>
    /// <returns>表示异步删除流程的任务。/ A task representing the asynchronous deletion flow.</returns>
    /// <remarks>
    /// zh-CN: 该方法会先删除依赖边，再清空子任务父引用，最后批量删除任务节点，以避免关系约束阻塞删除。
    /// en: This method deletes dependency edges first, nulls child-task parent links next, and finally deletes the task nodes so relational constraints do not block the cleanup.
    /// </remarks>
    private async Task DeleteTaskGraphAsync(Guid projectId, CancellationToken ct)
    {
        var taskIds = await _tasks
            .Where(task => task.ProjectId == projectId)
            .Select(task => task.Id)
            .ToListAsync(ct);
        if (taskIds.Count == 0)
            return;

        await _taskDependencies
            .Where(dependency => taskIds.Contains(dependency.TaskId) || taskIds.Contains(dependency.DependsOnId))
            .ExecuteDeleteAsync(ct);

        await _tasks
            .Where(task => task.ProjectId == projectId && task.ParentTaskId != null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(task => task.ParentTaskId, task => (Guid?)null),
                ct);

        await _tasks
            .Where(task => task.ProjectId == projectId)
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// 删除项目的智能体事件图谱。
    /// Deletes the project's agent-event graph.
    /// </summary>
    /// <param name="projectId">项目标识。/ Project identifier.</param>
    /// <param name="ct">取消令牌。/ Cancellation token.</param>
    /// <returns>表示异步删除流程的任务。/ A task representing the asynchronous deletion flow.</returns>
    /// <remarks>
    /// zh-CN: 该方法会先断开父子事件引用，再批量删除事件记录，确保图状关系在数据库中可安全移除。
    /// en: This method severs parent-child event references before bulk-deleting event records, ensuring the graph can be removed safely in the database.
    /// </remarks>
    private async Task DeleteAgentEventGraphAsync(Guid projectId, CancellationToken ct)
    {
        await _agentEvents
            .Where(agentEvent => agentEvent.ProjectId == projectId && agentEvent.ParentEventId != null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(agentEvent => agentEvent.ParentEventId, agentEvent => (Guid?)null),
                ct);

        await _agentEvents
            .Where(agentEvent => agentEvent.ProjectId == projectId)
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// 获取项目脑暴文档在工作区中的完整路径。
    /// Gets the full workspace path of the project's brainstorm document.
    /// </summary>
    /// <param name="project">项目实体。/ Project entity.</param>
    /// <returns>脑暴文档的完整路径。/ Full path to the brainstorm document.</returns>
    /// <remarks>
    /// zh-CN: 该方法只计算路径，不执行文件系统读写；若工作区尚未初始化则抛出异常，防止后续文件操作写入未知位置。
    /// en: This method computes the path only and performs no filesystem I/O; it throws when the workspace is not initialized so later file operations cannot target an unknown location.
    /// </remarks>
    private static string GetBrainstormDocumentPath(Project project)
    {
        if (string.IsNullOrWhiteSpace(project.WorkspacePath))
            throw new InvalidOperationException($"Project {project.Id} workspace is not initialized");

        return Path.Combine(project.WorkspacePath, ".staff", "project-brainstorm.md");
    }

    /// <summary>
    /// 规范化脑暴文档 Markdown 内容。
    /// Normalizes brainstorm-document Markdown content.
    /// </summary>
    /// <param name="documentMarkdown">原始文档内容。/ Raw document content.</param>
    /// <returns>统一换行并补齐结尾换行的 Markdown。/ Markdown with normalized line endings and a trailing newline.</returns>
    /// <remarks>
    /// zh-CN: 该方法会在持久化前统一使用 <c>\n</c> 换行，并强制追加末尾换行，便于后续文件写入和差异比较保持稳定。
    /// en: This method normalizes line endings to <c>\n</c> and enforces a trailing newline before persistence so later file writes and diffs stay stable.
    /// </remarks>
    private static string NormalizeDocumentMarkdown(string? documentMarkdown)
    {
        if (string.IsNullOrWhiteSpace(documentMarkdown))
            throw new InvalidOperationException("ProjectBrainstorm state requires a non-empty documentMarkdown");

        var normalized = documentMarkdown.Replace("\r\n", "\n");
        return normalized.EndsWith('\n') ? normalized : normalized + "\n";
    }

    /// <summary>
    /// 规范化并校验脑暴阶段值。
    /// Normalizes and validates the brainstorm phase value.
    /// </summary>
    /// <param name="phase">待规范化的阶段文本。/ Phase text to normalize.</param>
    /// <returns>允许的标准阶段；若输入为空则返回 <see langword="null" />。/ The allowed canonical phase, or <see langword="null" /> when the input is empty.</returns>
    /// <remarks>
    /// zh-CN: 该解析仅接受系统支持的脑暴阶段，未知值会立即抛出异常，避免把无效状态写入项目生命周期。
    /// en: This parser accepts only supported brainstorm phases and throws immediately for unknown values so invalid state never enters the project lifecycle.
    /// </remarks>
    private static string? NormalizeBrainstormPhase(string? phase) => phase switch
    {
        null or "" => null,
        ProjectPhases.Brainstorming => ProjectPhases.Brainstorming,
        ProjectPhases.ReadyToStart => ProjectPhases.ReadyToStart,
        _ => throw new InvalidOperationException($"Unsupported ProjectBrainstorm phase '{phase}'")
    };
}

/// <summary>
/// ProjectBrainstorm 状态应用结果。
/// Result returned after applying ProjectBrainstorm state.
/// </summary>
/// <param name="DisplayContent">展示给用户的文本。 / Text displayed to the user.</param>
/// <param name="DocumentUpdated">文档是否发生变化。 / Whether the document changed.</param>
/// <param name="PhaseChanged">项目阶段是否发生变化。 / Whether the project phase changed.</param>
/// <param name="CurrentPhase">应用后的当前阶段。 / Current phase after applying the state.</param>
public sealed record ProjectBrainstormApplyResult(
    string DisplayContent,
    bool DocumentUpdated,
    bool PhaseChanged,
    string CurrentPhase);

internal sealed class ProjectBrainstormStateEnvelope
{
    private const string OpenTag = "<openstaff_brainstorm_state>";
    private const string CloseTag = "</openstaff_brainstorm_state>";

    public string DocumentMarkdown { get; init; } = string.Empty;
    public string? Phase { get; init; }

    /// <summary>
    /// 从智能体回复中提取隐藏的脑暴状态块。
    /// Extracts the hidden brainstorm state block from an agent response.
    /// </summary>
    public static bool TryExtract(string? content, out string displayContent, out ProjectBrainstormStateEnvelope? envelope)
    {
        displayContent = content?.Trim() ?? string.Empty;
        envelope = null;

        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var start = content.IndexOf(OpenTag, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        var end = content.IndexOf(CloseTag, start + OpenTag.Length, StringComparison.Ordinal);
        if (end < 0)
            throw new InvalidOperationException("ProjectBrainstorm state block is missing the closing tag");

        var json = content.Substring(start + OpenTag.Length, end - start - OpenTag.Length).Trim();
        var before = content[..start].Trim();
        var after = content[(end + CloseTag.Length)..].Trim();

        // zh-CN: 标签内的 JSON 负责落盘状态，标签外的自然语言继续返回给前端作为可见回复。
        // en: JSON inside the tags is persisted as state, while natural language outside the tags remains visible to the frontend.
        displayContent = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            new[] { before, after }.Where(part => !string.IsNullOrWhiteSpace(part)));

        if (string.IsNullOrWhiteSpace(displayContent))
        {
            displayContent = "已根据当前讨论更新项目概述。";
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var documentMarkdown = root.TryGetProperty("documentMarkdown", out var explicitDocument)
            ? explicitDocument.GetString()
            : root.TryGetProperty("document", out var legacyDocument)
                ? legacyDocument.GetString()
                : null;

        envelope = new ProjectBrainstormStateEnvelope
        {
            DocumentMarkdown = documentMarkdown ?? throw new InvalidOperationException("ProjectBrainstorm state is missing documentMarkdown"),
            Phase = root.TryGetProperty("phase", out var phase) ? phase.GetString() : null
        };
        return true;
    }
}

/// <summary>
/// 创建项目的内部请求模型。
/// Internal request model used when creating a project.
/// </summary>
public class CreateProjectRequest
{
    /// <summary>项目名称。 / Project name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>项目描述。 / Project description.</summary>
    public string? Description { get; set; }

    /// <summary>默认语言。 / Default language.</summary>
    public string? Language { get; set; }

    /// <summary>默认提供商标识。 / Default provider identifier.</summary>
    public Guid? DefaultProviderId { get; set; }

    /// <summary>默认模型名称。 / Default model name.</summary>
    public string? DefaultModelName { get; set; }
}

/// <summary>
/// 更新项目的内部请求模型。
/// Internal request model used when updating a project.
/// </summary>
public class UpdateProjectRequest
{
    /// <summary>项目名称。 / Project name.</summary>
    public string? Name { get; set; }

    /// <summary>项目描述。 / Project description.</summary>
    public string? Description { get; set; }

    /// <summary>默认语言。 / Default language.</summary>
    public string? Language { get; set; }

    /// <summary>默认提供商标识。 / Default provider identifier.</summary>
    public Guid? DefaultProviderId { get; set; }

    /// <summary>默认模型名称。 / Default model name.</summary>
    public string? DefaultModelName { get; set; }

    /// <summary>扩展配置 JSON。 / Additional configuration JSON.</summary>
    public string? ExtraConfig { get; set; }
}

