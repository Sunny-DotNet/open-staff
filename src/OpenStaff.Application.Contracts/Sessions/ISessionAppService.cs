using OpenStaff.Application.Contracts.Sessions.Dtos;

namespace OpenStaff.Application.Contracts.Sessions;

public interface ISessionAppService
{
    Task<CreateSessionOutput> CreateAsync(CreateSessionInput input, CancellationToken ct = default);
    Task<SendMessageOutput> SendMessageAsync(Guid sessionId, string input, CancellationToken ct = default);
    Task<SessionDto?> GetByIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<SessionEventDto>> GetEventsAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<ChatMessageDto>> GetFrameMessagesAsync(Guid sessionId, Guid frameId, CancellationToken ct = default);
    Task CancelAsync(Guid sessionId, CancellationToken ct = default);
    Task PopFrameAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<SessionDto>> GetByProjectAsync(Guid projectId, int limit = 20, CancellationToken ct = default);
    Task<ChatMessageListOutput> GetChatMessagesAsync(Guid sessionId, int skip = 0, int take = 50, CancellationToken ct = default);
}
