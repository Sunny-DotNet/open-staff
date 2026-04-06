using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Contracts.Sessions;
using OpenStaff.Application.Contracts.Sessions.Dtos;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly ISessionAppService _sessionAppService;

    public SessionsController(ISessionAppService sessionAppService)
    {
        _sessionAppService = sessionAppService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSessionInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Input))
            return BadRequest(new { message = "输入不能为空" });

        var result = await _sessionAppService.CreateAsync(input, ct);
        return Ok(result);
    }

    [HttpPost("{sessionId:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid sessionId, [FromBody] ChatMessageRequest body, CancellationToken ct)
    {
        var result = await _sessionAppService.SendMessageAsync(new SendSessionMessageRequest
        {
            SessionId = sessionId,
            Input = body.Input
        }, ct);
        return Ok(result);
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> GetSession(Guid sessionId, CancellationToken ct)
    {
        var result = await _sessionAppService.GetByIdAsync(sessionId, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("{sessionId:guid}/events")]
    public async Task<IActionResult> GetEvents(Guid sessionId, CancellationToken ct)
        => Ok(await _sessionAppService.GetEventsAsync(sessionId, ct));

    [HttpGet("{sessionId:guid}/frames/{frameId:guid}/messages")]
    public async Task<IActionResult> GetFrameMessages(Guid sessionId, Guid frameId, CancellationToken ct)
        => Ok(await _sessionAppService.GetFrameMessagesAsync(new GetFrameMessagesRequest { SessionId = sessionId, FrameId = frameId }, ct));

    [HttpPost("{sessionId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid sessionId, CancellationToken ct)
    {
        await _sessionAppService.CancelAsync(sessionId, ct);
        return Ok(new { status = "cancelling" });
    }

    [HttpPost("{sessionId:guid}/pop")]
    public async Task<IActionResult> PopFrame(Guid sessionId, CancellationToken ct)
    {
        await _sessionAppService.PopFrameAsync(sessionId, ct);
        return Ok(new { status = "popping" });
    }

    [HttpGet("by-project/{projectId:guid}")]
    public async Task<IActionResult> GetByProject(Guid projectId, [FromQuery] int limit = 20, CancellationToken ct = default)
        => Ok(await _sessionAppService.GetByProjectAsync(new GetSessionsByProjectRequest { ProjectId = projectId, Limit = limit }, ct));

    [HttpGet("{sessionId:guid}/chat-messages")]
    public async Task<IActionResult> GetChatMessages(Guid sessionId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(await _sessionAppService.GetChatMessagesAsync(new GetChatMessagesRequest { SessionId = sessionId, Skip = skip, Take = take }, ct));
}

public class ChatMessageRequest
{
    public string Input { get; set; } = string.Empty;
}
