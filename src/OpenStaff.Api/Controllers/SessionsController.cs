using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Application.Sessions;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly SessionRunner _runner;
    private readonly SessionStreamManager _streamManager;
    private readonly AppDbContext _db;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        SessionRunner runner,
        SessionStreamManager streamManager,
        AppDbContext db,
        ILogger<SessionsController> logger)
    {
        _runner = runner;
        _streamManager = streamManager;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 创建新会话 — 异步启动，返回 sessionId 供客户端订阅
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
            return BadRequest(new { error = "Input is required" });

        var session = await _runner.StartSessionAsync(
            request.ProjectId,
            request.Input,
            request.ContextStrategy ?? ContextStrategies.Full);

        return Ok(new
        {
            sessionId = session.Id,
            status = session.Status,
            createdAt = session.CreatedAt
        });
    }

    /// <summary>
    /// 群聊追加消息 — 向已有 Session 发送新消息
    /// </summary>
    [HttpPost("{sessionId}/messages")]
    public async Task<IActionResult> SendMessage(Guid sessionId, [FromBody] ChatMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
            return BadRequest(new { error = "Input is required" });

        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session == null)
            return NotFound(new { error = "Session not found" });

        await _runner.SendMessageAsync(sessionId, request.Input);

        return Ok(new
        {
            sessionId,
            status = "message_sent",
            isAwaitingInput = _runner.IsAwaitingInput(sessionId)
        });
    }

    /// <summary>
    /// 获取会话详情
    /// </summary>
    [HttpGet("{sessionId}")]
    public async Task<IActionResult> GetSession(Guid sessionId)
    {
        var session = await _db.ChatSessions
            .Include(s => s.Frames)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            return NotFound(new { error = "Session not found" });

        return Ok(new
        {
            id = session.Id,
            projectId = session.ProjectId,
            status = session.Status,
            initialInput = session.InitialInput,
            finalResult = session.FinalResult,
            contextStrategy = session.ContextStrategy,
            createdAt = session.CreatedAt,
            completedAt = session.CompletedAt,
            isActive = _streamManager.IsActive(session.Id),
            frames = session.Frames.OrderBy(f => f.Depth).Select(f => new
            {
                id = f.Id,
                parentFrameId = f.ParentFrameId,
                depth = f.Depth,
                initiatorRole = f.InitiatorRole,
                targetRole = f.TargetRole,
                purpose = f.Purpose,
                status = f.Status,
                result = f.Result
            })
        });
    }

    /// <summary>
    /// 获取会话历史事件（从数据库，用于已完成的会话）
    /// </summary>
    [HttpGet("{sessionId}/events")]
    public async Task<IActionResult> GetSessionEvents(Guid sessionId)
    {
        var events = await _db.SessionEvents
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.SequenceNo)
            .Select(e => new
            {
                id = e.Id,
                frameId = e.FrameId,
                eventType = e.EventType,
                payload = e.Payload,
                sequenceNo = e.SequenceNo,
                createdAt = e.CreatedAt
            })
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>
    /// 获取帧内消息
    /// </summary>
    [HttpGet("{sessionId}/frames/{frameId}/messages")]
    public async Task<IActionResult> GetFrameMessages(Guid sessionId, Guid frameId)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.SessionId == sessionId && m.FrameId == frameId)
            .OrderBy(m => m.SequenceNo)
            .Select(m => new
            {
                id = m.Id,
                role = m.Role,
                agentRole = m.AgentRole,
                content = m.Content,
                contentType = m.ContentType,
                sequenceNo = m.SequenceNo,
                tokenUsage = m.TokenUsage,
                durationMs = m.DurationMs,
                createdAt = m.CreatedAt
            })
            .ToListAsync();

        return Ok(messages);
    }

    /// <summary>
    /// 取消整个会话
    /// </summary>
    [HttpPost("{sessionId}/cancel")]
    public async Task<IActionResult> CancelSession(Guid sessionId)
    {
        await _runner.CancelSessionAsync(sessionId);
        return Ok(new { message = "Session cancellation requested" });
    }

    /// <summary>
    /// Pop 当前 Frame（只取消当前帧处理）
    /// </summary>
    [HttpPost("{sessionId}/pop")]
    public IActionResult PopCurrentFrame(Guid sessionId)
    {
        _runner.PopCurrentFrame(sessionId);
        return Ok(new { message = "Frame pop requested" });
    }

    /// <summary>
    /// 获取项目的会话列表
    /// </summary>
    [HttpGet("by-project/{projectId}")]
    public async Task<IActionResult> GetProjectSessions(Guid projectId, [FromQuery] int limit = 20)
    {
        var sessions = await _db.ChatSessions
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .Select(s => new
            {
                id = s.Id,
                status = s.Status,
                initialInput = s.InitialInput,
                createdAt = s.CreatedAt,
                completedAt = s.CompletedAt,
                isActive = _streamManager.IsActive(s.Id)
            })
            .ToListAsync();

        return Ok(sessions);
    }

    /// <summary>
    /// 获取群聊消息流 — 按时间排序的所有消息（分页）
    /// </summary>
    [HttpGet("{sessionId}/chat-messages")]
    public async Task<IActionResult> GetChatMessages(Guid sessionId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(m => new
            {
                id = m.Id,
                frameId = m.FrameId,
                role = m.Role,
                agentRole = m.AgentRole,
                content = m.Content,
                contentType = m.ContentType,
                createdAt = m.CreatedAt
            })
            .ToListAsync();

        var total = await _db.ChatMessages.CountAsync(m => m.SessionId == sessionId);

        return Ok(new { messages, total, skip, take });
    }
}

public class CreateSessionRequest
{
    public Guid ProjectId { get; set; }
    public string Input { get; set; } = string.Empty;
    public string? ContextStrategy { get; set; }
}

public class ChatMessageRequest
{
    public string Input { get; set; } = string.Empty;
}
