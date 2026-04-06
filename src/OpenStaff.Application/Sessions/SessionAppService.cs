using Microsoft.EntityFrameworkCore;
using OpenStaff.Application.Contracts.Sessions;
using OpenStaff.Application.Contracts.Sessions.Dtos;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Application.Sessions;

public class SessionAppService : ISessionAppService
{
    private readonly SessionRunner _runner;
    private readonly SessionStreamManager _streamManager;
    private readonly AppDbContext _db;

    public SessionAppService(
        SessionRunner runner,
        SessionStreamManager streamManager,
        AppDbContext db)
    {
        _runner = runner;
        _streamManager = streamManager;
        _db = db;
    }

    public async Task<CreateSessionOutput> CreateAsync(CreateSessionInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Input))
            throw new ArgumentException("Input is required");

        var session = await _runner.StartSessionAsync(
            input.ProjectId,
            input.Input,
            input.ContextStrategy ?? ContextStrategies.Full);

        return new CreateSessionOutput
        {
            SessionId = session.Id,
            Status = session.Status
        };
    }

    public async Task<SendMessageOutput> SendMessageAsync(Guid sessionId, string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input is required");

        var session = await _db.ChatSessions.FindAsync(new object[] { sessionId }, ct);
        if (session == null) throw new KeyNotFoundException("Session not found");

        await _runner.SendMessageAsync(sessionId, input);

        return new SendMessageOutput
        {
            Status = "message_sent",
            IsAwaitingInput = _runner.IsAwaitingInput(sessionId)
        };
    }

    public async Task<SessionDto?> GetByIdAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _db.ChatSessions
            .Include(s => s.Frames)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null) return null;

        return new SessionDto
        {
            Id = session.Id,
            ProjectId = session.ProjectId,
            Status = session.Status,
            Input = session.InitialInput,
            Result = session.FinalResult,
            ContextStrategy = session.ContextStrategy,
            CreatedAt = session.CreatedAt,
            CompletedAt = session.CompletedAt,
            IsActive = _streamManager.IsActive(session.Id),
            Frames = session.Frames.OrderBy(f => f.Depth).Select(f => new SessionFrameDto
            {
                Id = f.Id,
                AgentRole = f.TargetRole,
                Status = f.Status,
                Order = f.Depth,
                CreatedAt = f.CreatedAt
            }).ToList()
        };
    }

    public async Task<List<SessionEventDto>> GetEventsAsync(Guid sessionId, CancellationToken ct)
    {
        return await _db.SessionEvents
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.SequenceNo)
            .Select(e => new SessionEventDto
            {
                Id = e.Id,
                EventType = e.EventType,
                Data = e.Payload,
                SequenceNo = (int)e.SequenceNo,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<List<ChatMessageDto>> GetFrameMessagesAsync(Guid sessionId, Guid frameId, CancellationToken ct)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.SessionId == sessionId && m.FrameId == frameId)
            .OrderBy(m => m.SequenceNo)
            .ToListAsync(ct);

        return messages.Select(m => new ChatMessageDto
        {
            Role = m.Role,
            AgentRole = m.AgentRole,
            Content = m.Content,
            ContentType = m.ContentType,
            TokenUsage = int.TryParse(m.TokenUsage, out var tu) ? tu : null,
            DurationMs = m.DurationMs,
            CreatedAt = m.CreatedAt
        }).ToList();
    }

    public async Task CancelAsync(Guid sessionId, CancellationToken ct)
    {
        await _runner.CancelSessionAsync(sessionId);
    }

    public Task PopFrameAsync(Guid sessionId, CancellationToken ct)
    {
        _runner.PopCurrentFrame(sessionId);
        return Task.CompletedTask;
    }

    public async Task<List<SessionDto>> GetByProjectAsync(Guid projectId, int limit, CancellationToken ct)
    {
        return await _db.ChatSessions
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .Select(s => new SessionDto
            {
                Id = s.Id,
                ProjectId = s.ProjectId,
                Status = s.Status,
                Input = s.InitialInput,
                CreatedAt = s.CreatedAt,
                CompletedAt = s.CompletedAt,
                IsActive = _streamManager.IsActive(s.Id)
            })
            .ToListAsync(ct);
    }

    public async Task<ChatMessageListOutput> GetChatMessagesAsync(Guid sessionId, int skip, int take, CancellationToken ct)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(m => new ChatMessageDto
            {
                Role = m.Role,
                AgentRole = m.AgentRole,
                Content = m.Content,
                ContentType = m.ContentType,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);

        var total = await _db.ChatMessages.CountAsync(m => m.SessionId == sessionId, ct);

        return new ChatMessageListOutput
        {
            Messages = messages,
            Total = total
        };
    }
}
