using OpenStaff.Application.Contracts.Sessions.Dtos;

namespace OpenStaff.Application.Contracts.Sessions;

public interface ISessionAppService
{
    Task<CreateSessionOutput> CreateAsync(CreateSessionInput input, CancellationToken ct = default);
    Task<SendMessageOutput> SendMessageAsync(SendSessionMessageRequest request, CancellationToken ct = default);
    Task<SessionDto?> GetByIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<SessionEventDto>> GetEventsAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<ChatMessageDto>> GetFrameMessagesAsync(GetFrameMessagesRequest request, CancellationToken ct = default);
    Task CancelAsync(Guid sessionId, CancellationToken ct = default);
    Task PopFrameAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<SessionDto>> GetByProjectAsync(GetSessionsByProjectRequest request, CancellationToken ct = default);
    Task<ChatMessageListOutput> GetChatMessagesAsync(GetChatMessagesRequest request, CancellationToken ct = default);
}
