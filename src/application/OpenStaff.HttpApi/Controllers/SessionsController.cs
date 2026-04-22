
using Microsoft.AspNetCore.Mvc;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// 会话管理控制器。
/// Controller that exposes chat session creation, messaging, and stack control endpoints.
/// </summary>
[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly ISessionApiService _sessionApiService;

    /// <summary>
    /// 初始化会话管理控制器。
    /// Initializes the session management controller.
    /// </summary>
    public SessionsController(ISessionApiService sessionApiService)
    {
        _sessionApiService = sessionApiService;
    }

    /// <summary>
    /// 创建会话。
    /// Creates a session.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ConversationTaskOutput>> Create([FromBody] CreateSessionInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Input) && string.IsNullOrWhiteSpace(input.RawInput))
            return BadRequest(new ApiMessageDto { Message = "输入不能为空" });

        var result = await _sessionApiService.CreateAsync(input, ct);
        return Ok(result);
    }

    /// <summary>
    /// 向会话发送新消息。
    /// Sends a new message to a session.
    /// </summary>
    [HttpPost("{sessionId:guid}/messages")]
    public async Task<ActionResult<ConversationTaskOutput>> SendMessage(Guid sessionId, [FromBody] ChatMessageRequest body, CancellationToken ct)
    {
        var result = await _sessionApiService.SendMessageAsync(new SendSessionMessageRequest
        {
            SessionId = sessionId,
            Input = body.Input,
            RawInput = body.RawInput,
            Mentions = body.Mentions
        }, ct);
        return Ok(result);
    }

    /// <summary>
    /// 获取会话详情。
    /// Gets the details of a session.
    /// </summary>
    [HttpGet("{sessionId:guid}")]
    public async Task<ActionResult<SessionDto?>> GetSession(Guid sessionId, CancellationToken ct)
    {
        var result = await _sessionApiService.GetByIdAsync(sessionId, ct);
        return Ok(result);
    }

    /// <summary>
    /// 获取会话事件列表。
    /// Gets the event list for a session.
    /// </summary>
    [HttpGet("{sessionId:guid}/events")]
    public async Task<ActionResult<List<SessionEventDto>>> GetEvents(Guid sessionId, CancellationToken ct)
        => Ok(await _sessionApiService.GetEventsAsync(sessionId, ct));

    /// <summary>
    /// 获取指定帧的消息列表。
    /// Gets the messages that belong to a specific frame.
    /// </summary>
    [HttpGet("{sessionId:guid}/frames/{frameId:guid}/messages")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetFrameMessages(Guid sessionId, Guid frameId, CancellationToken ct)
        => Ok(await _sessionApiService.GetFrameMessagesAsync(new GetFrameMessagesRequest { SessionId = sessionId, FrameId = frameId }, ct));

    /// <summary>
    /// 取消整个会话。
    /// Cancels the entire session.
    /// </summary>
    [HttpPost("{sessionId:guid}/cancel")]
    public async Task<ActionResult<ApiStatusDto>> Cancel(Guid sessionId, CancellationToken ct)
    {
        await _sessionApiService.CancelAsync(sessionId, ct);
        return Ok(new ApiStatusDto { Status = "cancelling" });
    }

    /// <summary>
    /// 弹出当前活动帧。
    /// Pops the currently active frame.
    /// </summary>
    [HttpPost("{sessionId:guid}/pop")]
    public async Task<ActionResult<ApiStatusDto>> PopFrame(Guid sessionId, CancellationToken ct)
    {
        await _sessionApiService.PopFrameAsync(sessionId, ct);
        return Ok(new ApiStatusDto { Status = "popping" });
    }

    /// <summary>
    /// 获取项目下的近期会话列表。
    /// Gets recent sessions for a project.
    /// </summary>
    [HttpGet("by-project/{projectId:guid}")]
    public async Task<ActionResult<List<SessionDto>>> GetByProject(Guid projectId, [FromQuery] int limit = 20, [FromQuery] string? scene = null, CancellationToken ct = default)
        => Ok(await _sessionApiService.GetByProjectAsync(new GetSessionsByProjectRequest { ProjectId = projectId, Limit = limit, Scene = scene }, ct));

    /// <summary>
    /// 获取项目指定场景的活跃会话。
    /// Gets the active session for a project scene.
    /// </summary>
    [HttpGet("by-project/{projectId:guid}/active")]
    public async Task<ActionResult<SessionDto?>> GetActiveByScene(Guid projectId, [FromQuery] string scene, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scene))
            return BadRequest(new ApiMessageDto { Message = "scene 不能为空" });

        var result = await _sessionApiService.GetActiveBySceneAsync(new GetActiveProjectSessionRequest
        {
            ProjectId = projectId,
            Scene = scene
        }, ct);

        return Ok(result);
    }

    /// <summary>
    /// 分页获取对外可见的聊天消息。
    /// Gets visible chat messages with paging.
    /// </summary>
    [HttpGet("{sessionId:guid}/chat-messages")]
    public async Task<ActionResult<ChatMessageListOutput>> GetChatMessages(Guid sessionId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(await _sessionApiService.GetChatMessagesAsync(new GetChatMessagesRequest { SessionId = sessionId, Skip = skip, Take = take }, ct));
}

/// <summary>
/// 会话消息请求体。
/// Request body used to send a session message.
/// </summary>
public class ChatMessageRequest
{
    /// <summary>消息正文。 / Message body.</summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>原始展示文本。 / Original display text before backend normalization.</summary>
    public string? RawInput { get; set; }

    /// <summary>结构化提及信息。 / Structured mention metadata.</summary>
    public List<ConversationMentionDto>? Mentions { get; set; }
}

