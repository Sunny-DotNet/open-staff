using OpenStaff.Application.Sessions.Services;
using OpenStaff.Application.Conversations.Models;
using OpenStaff.Application.Conversations.Services;

namespace OpenStaff.ApiServices;
/// <summary>
/// 会话应用服务实现。
/// Application service implementation for session lifecycle and history access.
/// </summary>
public class SessionApiService : ApiServiceBase, ISessionApiService
{
    private static readonly JsonSerializerOptions UsageJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SessionRunner _runner;
    private readonly SessionStreamManager _streamManager;
    private readonly IChatSessionRepository _chatSessions;
    private readonly IChatMessageRepository _chatMessages;
    private readonly ISessionEventRepository _sessionEvents;
    private readonly IProjectRepository _projects;
    private readonly ConversationEntryService? _conversationEntryService;

    /// <summary>
    /// 初始化会话应用服务。
    /// Initializes the session application service.
    /// </summary>
    public SessionApiService(
        SessionRunner runner,
        SessionStreamManager streamManager,
        IChatSessionRepository chatSessions,
        IChatMessageRepository chatMessages,
        ISessionEventRepository sessionEvents,
        IProjectRepository projects,
        ConversationEntryService? conversationEntryService = null,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _runner = runner;
        _streamManager = streamManager;
        _chatSessions = chatSessions;
        _chatMessages = chatMessages;
        _sessionEvents = sessionEvents;
        _projects = projects;
        _conversationEntryService = conversationEntryService;
    }

    /// <inheritdoc />
    public async Task<ConversationTaskOutput> CreateAsync(CreateSessionInput input, CancellationToken ct)
    {
        var (executionInput, rawInput) = NormalizeConversationInputs(input.Input, input.RawInput);

        var scene = NormalizeScene(input.Scene);
        await ValidateSceneAccessAsync(input.ProjectId, scene, ct);
        return _conversationEntryService == null
            ? await _runner.StartSessionTaskAsync(
                input.ProjectId,
                executionInput,
                input.ContextStrategy ?? ContextStrategies.Full,
                scene,
                MapMentions(input.Mentions),
                rawInput)
            : await CreateViaConversationEntryAsync(input, scene, executionInput, rawInput, ct);
    }

    /// <inheritdoc />
    public async Task<ConversationTaskOutput> SendMessageAsync(SendSessionMessageRequest request, CancellationToken ct)
    {
        var (executionInput, rawInput) = NormalizeConversationInputs(request.Input, request.RawInput);

        var session = await _chatSessions.FindAsync(request.SessionId, ct);
        if (session == null) throw new KeyNotFoundException("Session not found");
        await ValidateSessionWritableAsync(session, ct);

        if (_conversationEntryService == null)
        {
            return await _runner.SendMessageTaskAsync(request.SessionId, executionInput, MapMentions(request.Mentions), rawInput);
        }

        return await _conversationEntryService.SendSessionReplyAsync(
            new SessionReplyEntry(request.SessionId, executionInput, MapMentions(request.Mentions))
            {
                RawInput = rawInput
            },
            ct);
    }

    /// <inheritdoc />
    public async Task<SessionDto?> GetByIdAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _chatSessions
            .AsNoTracking()
            .Include(s => s.Frames)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null)
            return null;

        var frameEntries = await _chatMessages
            .AsNoTracking()
            .Where(message => message.SessionId == sessionId)
            .OrderBy(message => message.FrameId)
            .ThenBy(message => message.SequenceNo)
            .Select(message => new FrameEntryMessage(message.FrameId, message.Id, message.ParentMessageId))
            .ToListAsync(ct);

        var frameEntryLookup = frameEntries
            .GroupBy(message => message.FrameId)
            .ToDictionary(group => group.Key, group => group.First());

        return MapToDto(session, includeFrames: true, frameEntryLookup);
    }

    /// <inheritdoc />
    public async Task<SessionDto?> GetActiveBySceneAsync(GetActiveProjectSessionRequest request, CancellationToken ct)
    {
        var scene = NormalizeScene(request.Scene);
        var session = await _chatSessions
            .AsNoTracking()
            .Where(s => s.ProjectId == request.ProjectId
                && s.Scene == scene
                && (s.Status == SessionStatus.Active || s.Status == SessionStatus.AwaitingInput))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return session == null ? null : MapToDto(session);
    }

    /// <inheritdoc />
    public async Task<List<SessionEventDto>> GetEventsAsync(Guid sessionId, CancellationToken ct)
    {
        var activeStream = _streamManager.GetActive(sessionId);
        if (activeStream != null)
        {
            var bufferedEvents = activeStream.GetBufferedEvents();
            if (bufferedEvents.Count > 0)
            {
                return bufferedEvents
                    .OrderBy(e => e.SequenceNo)
                    .Select(MapEvent)
                    .ToList();
            }
        }

        return await _sessionEvents
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.SequenceNo)
            .Select(e => MapEvent(e))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<ChatMessageDto>> GetFrameMessagesAsync(GetFrameMessagesRequest request, CancellationToken ct)
    {
        var messages = await _chatMessages
            .Where(m => m.SessionId == request.SessionId && m.FrameId == request.FrameId)
            .OrderBy(m => m.SequenceNo)
            .ToListAsync(ct);

        return messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            SessionId = m.SessionId,
            FrameId = m.FrameId,
            ExecutionPackageId = m.ExecutionPackageId,
            OriginatingFrameId = m.OriginatingFrameId,
            ParentMessageId = m.ParentMessageId,
            Role = m.Role,
            AgentRoleId = m.AgentRoleId,
            ProjectAgentRoleId = m.ProjectAgentRoleId,
            Content = m.Content,
            ContentType = m.ContentType,
            TokenUsage = ParseTotalTokens(m.TokenUsage),
            DurationMs = m.DurationMs,
            Usage = ParseUsage(m.TokenUsage),
            Timing = ParseTiming(m.DurationMs),
            CreatedAt = m.CreatedAt
        }).ToList();
    }

    /// <inheritdoc />
    public async Task CancelAsync(Guid sessionId, CancellationToken ct)
    {
        await _runner.CancelSessionAsync(sessionId);
    }

    /// <inheritdoc />
    public Task PopFrameAsync(Guid sessionId, CancellationToken ct)
    {
        _runner.PopCurrentFrame(sessionId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<List<SessionDto>> GetByProjectAsync(GetSessionsByProjectRequest request, CancellationToken ct)
    {
        var query = _chatSessions
            .AsNoTracking()
            .Where(s => s.ProjectId == request.ProjectId);

        if (!string.IsNullOrWhiteSpace(request.Scene))
        {
            var scene = NormalizeScene(request.Scene);
            query = query.Where(s => s.Scene == scene);
        }

        var sessions = await query
            .OrderByDescending(s => s.CreatedAt)
            .Take(request.Limit)
            .ToListAsync(ct);

        return sessions.Select(s => MapToDto(s)).ToList();
    }

    /// <inheritdoc />
    public async Task<ChatMessageListOutput> GetChatMessagesAsync(GetChatMessagesRequest request, CancellationToken ct)
    {
        var messages = await _chatMessages
            .Where(m => m.SessionId == request.SessionId && m.ContentType != MessageContentTypes.Internal)
            .OrderBy(m => m.CreatedAt)
            .Skip(request.Skip)
            .Take(request.Take)
            .ToListAsync(ct);

        var total = await _chatMessages.CountAsync(
            m => m.SessionId == request.SessionId && m.ContentType != MessageContentTypes.Internal,
            ct);

        return new ChatMessageListOutput
        {
            Messages = messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SessionId = m.SessionId,
                FrameId = m.FrameId,
                ExecutionPackageId = m.ExecutionPackageId,
                OriginatingFrameId = m.OriginatingFrameId,
                ParentMessageId = m.ParentMessageId,
                Role = m.Role,
                AgentRoleId = m.AgentRoleId,
                ProjectAgentRoleId = m.ProjectAgentRoleId,
                Content = m.Content,
                ContentType = m.ContentType,
                TokenUsage = ParseTotalTokens(m.TokenUsage),
                DurationMs = m.DurationMs,
                Usage = ParseUsage(m.TokenUsage),
                Timing = ParseTiming(m.DurationMs),
                CreatedAt = m.CreatedAt
            }).ToList(),
            Total = total
        };
    }

    /// <summary>
    /// 将会话实体投影为 DTO，并在需要时按深度附带帧信息；若提供入口消息索引，还会补齐帧与消息之间的引用关系。
    /// Projects a session entity into its DTO and optionally includes depth-ordered frame data; when an entry-message lookup is supplied, it also reconstructs frame-to-message links.
    /// </summary>
    private SessionDto MapToDto(
        ChatSession session,
        bool includeFrames = false,
        IReadOnlyDictionary<Guid, FrameEntryMessage>? frameEntryLookup = null) => new()
    {
        Id = session.Id,
        ProjectId = session.ProjectId,
        Status = session.Status,
        Scene = session.Scene,
        Input = session.InitialInput,
        Result = session.FinalResult,
        ContextStrategy = session.ContextStrategy,
        CreatedAt = session.CreatedAt,
        CompletedAt = session.CompletedAt,
        IsActive = _streamManager.IsActive(session.Id),
        Frames = includeFrames
            ? session.Frames.OrderBy(f => f.Depth).Select(f => new SessionFrameDto
            {
                Id = f.Id,
                ParentFrameId = f.ParentFrameId,
                TaskId = f.TaskId,
                ExecutionPackageId = f.ExecutionPackageId,
                EntryMessageId = TryGetEntryMessage(frameEntryLookup, f.Id)?.MessageId,
                ParentMessageId = TryGetEntryMessage(frameEntryLookup, f.Id)?.ParentMessageId,
                AgentRoleId = f.TargetAgentRoleId,
                ProjectAgentRoleId = f.TargetProjectAgentRoleId,
                TargetAgentRoleId = f.TargetAgentRoleId,
                TargetProjectAgentRoleId = f.TargetProjectAgentRoleId,
                InitiatorAgentRoleId = f.InitiatorAgentRoleId,
                InitiatorProjectAgentRoleId = f.InitiatorProjectAgentRoleId,
                Purpose = f.Purpose,
                Status = f.Status,
                Result = f.Result,
                Depth = f.Depth,
                Order = f.Depth,
                CreatedAt = f.CreatedAt,
                CompletedAt = f.CompletedAt
            }).ToList()
            : null
    };

    private sealed record FrameEntryMessage(Guid FrameId, Guid MessageId, Guid? ParentMessageId);

    /// <summary>
    /// 安全读取帧入口消息索引，缺失时返回 <see langword="null"/>，避免 DTO 映射因为不完整查询结果而抛错。
    /// Safely reads the frame-entry lookup and returns <see langword="null"/> when the frame is absent so DTO mapping can tolerate partial query results.
    /// </summary>
    private static FrameEntryMessage? TryGetEntryMessage(
        IReadOnlyDictionary<Guid, FrameEntryMessage>? frameEntryLookup,
        Guid frameId)
    {
        if (frameEntryLookup != null && frameEntryLookup.TryGetValue(frameId, out var entry))
            return entry;

        return null;
    }

    /// <summary>
    /// 将场景字符串规范为已知枚举名称；未知值统一回退到 <c>ProjectBrainstorm</c>，保证持久化和查询键一致。
    /// Normalizes a scene string to a known enum name; unknown values fall back to <c>ProjectBrainstorm</c> so persistence and lookup keys stay consistent.
    /// </summary>
    private static string NormalizeScene(string? scene)
    {
        if (SessionSceneTypes.TryParse(scene, out var parsed))
        {
            return parsed.ToString();
        }

        return SessionSceneTypes.ProjectBrainstorm;
    }

    /// <summary>
    /// zh-CN: 把旧的 CreateSessionInput 映射到新的强类型入口模型。
    /// 这里显式按 scene 分支，而不是继续把 scene 原样传给底层，是为了尽早把“业务入口意图”编码成明确模型。
    /// 例如同样是 projectId + input：
    /// - ProjectBrainstorm 会固定成秘书私聊语义
    /// - ProjectGroup 会进入项目群聊编排语义
    /// en: Maps the legacy create-session DTO into the new strongly typed entry model.
    /// </summary>
    private Task<ConversationTaskOutput> CreateViaConversationEntryAsync(
        CreateSessionInput input,
        string scene,
        string executionInput,
        string rawInput,
        CancellationToken ct)
    {
        return scene switch
        {
            SessionSceneTypes.ProjectGroup => _conversationEntryService!.StartProjectGroupAsync(
                new ProjectGroupEntry(
                    input.ProjectId,
                    executionInput,
                    input.ContextStrategy ?? ContextStrategies.Full,
                    MapMentions(input.Mentions))
                {
                    RawInput = rawInput
                },
                ct),
            _ => _conversationEntryService!.StartProjectBrainstormAsync(
                new ProjectBrainstormEntry(
                    input.ProjectId,
                    executionInput,
                    input.ContextStrategy ?? ContextStrategies.Full),
                ct)
        };
    }

    private static (string ExecutionInput, string RawInput) NormalizeConversationInputs(string? input, string? rawInput)
    {
        var normalizedInput = input?.Trim();
        var normalizedRawInput = rawInput?.Trim();
        var executionInput = !string.IsNullOrWhiteSpace(normalizedInput)
            ? normalizedInput
            : normalizedRawInput;

        if (string.IsNullOrWhiteSpace(executionInput))
            throw new ArgumentException("Input is required");

        return (executionInput, string.IsNullOrWhiteSpace(normalizedRawInput) ? executionInput : normalizedRawInput);
    }

    private static IReadOnlyList<ConversationMention>? MapMentions(IReadOnlyList<ConversationMentionDto>? mentions)
    {
        return mentions?
            .Where(item => !string.IsNullOrWhiteSpace(item.RawText))
            .Select(item => new ConversationMention(
                item.RawText,
                item.ResolvedKind,
                item.BuiltinRole,
                item.ProjectAgentRoleId))
            .ToList();
    }

    /// <summary>
    /// 确认会话仍可写入，并在发送消息前重新校验场景访问权限；终态会话会直接拒绝。
    /// Ensures the session can still accept input and revalidates scene access before sending a message; terminal sessions are rejected immediately.
    /// </summary>
    private async Task ValidateSessionWritableAsync(ChatSession session, CancellationToken ct)
    {
        if (session.Status == SessionStatus.Completed || session.Status == SessionStatus.Cancelled || session.Status == SessionStatus.Error)
            throw new InvalidOperationException($"Session {session.Id} is {session.Status} and cannot accept new messages");

        await ValidateSceneAccessAsync(session.ProjectId, session.Scene, ct);
    }

    /// <summary>
    /// 根据项目阶段校验场景是否允许进入；该检查会查询项目状态，并在群聊或脑暴场景不匹配时抛出异常。
    /// Validates whether the requested scene is allowed for the project's current phase; it queries project state and throws when group-chat or brainstorm access is inconsistent.
    /// </summary>
    private async Task ValidateSceneAccessAsync(Guid projectId, string scene, CancellationToken ct)
    {
        var project = await _projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.Phase })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Project not found");

        if (scene == SessionSceneTypes.ProjectBrainstorm
            && (project.Phase == ProjectPhases.Running || project.Phase == ProjectPhases.Completed))
        {
            throw new InvalidOperationException("ProjectBrainstorm 已结束，当前只读");
        }

        if (scene == SessionSceneTypes.ProjectGroup
            && project.Phase != ProjectPhases.Running
            && project.Phase != ProjectPhases.Completed)
        {
            throw new InvalidOperationException("项目尚未启动，暂时不能进入项目群聊");
        }
    }

    /// <summary>
    /// 把消息用量从纯数字或 JSON 文本解析为结构化 DTO；对空值和脏数据保持宽容并返回 <see langword="null"/>。
    /// Parses message usage from either a plain token count or JSON into a structured DTO; empty or malformed payloads are tolerated and yield <see langword="null"/>.
    /// </summary>
    private static ChatMessageUsageDto? ParseUsage(string? tokenUsage)
    {
        if (string.IsNullOrWhiteSpace(tokenUsage))
            return null;

        if (int.TryParse(tokenUsage, out var totalTokens))
        {
            return new ChatMessageUsageDto
            {
                TotalTokens = totalTokens
            };
        }

        try
        {
            var usage = JsonSerializer.Deserialize<ChatMessageUsageDto>(tokenUsage, UsageJsonOptions);
            return usage is { InputTokens: null, OutputTokens: null, TotalTokens: null } ? null : usage;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 提取总 token 数，优先走纯数字快路径，再回退到完整用量解析。
    /// Extracts total tokens using a plain-number fast path before falling back to full usage parsing.
    /// </summary>
    private static int? ParseTotalTokens(string? tokenUsage)
    {
        if (int.TryParse(tokenUsage, out var totalTokens))
            return totalTokens;

        return ParseUsage(tokenUsage)?.TotalTokens;
    }

    /// <summary>
    /// 仅在存在耗时时构造时序 DTO，避免为未知时延伪造默认值。
    /// Builds a timing DTO only when a duration is present, avoiding fabricated defaults for unknown latency.
    /// </summary>
    private static ChatMessageTimingDto? ParseTiming(long? durationMs)
    {
        return durationMs.HasValue
            ? new ChatMessageTimingDto
            {
                TotalMs = durationMs.Value
            }
            : null;
    }

    /// <summary>
    /// 将持久化会话事件映射为 API DTO，并原样保留事件载荷与顺序信息。
    /// Maps a persisted session event to the API DTO while preserving payload and sequencing metadata verbatim.
    /// </summary>
    private static SessionEventDto MapEvent(SessionEvent e) => new()
    {
        Id = e.Id,
        SessionId = e.SessionId,
        FrameId = e.FrameId,
        MessageId = e.MessageId,
        ExecutionPackageId = e.ExecutionPackageId,
        SourceFrameId = e.SourceFrameId,
        SourceEffectIndex = e.SourceEffectIndex,
        EventType = e.EventType,
        Payload = e.Payload,
        SequenceNo = e.SequenceNo,
        CreatedAt = e.CreatedAt
    };
}



