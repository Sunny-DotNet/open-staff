using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// 会话应用服务契约。
/// Application service contract for chat session lifecycle and history access.
/// </summary>
public interface ISessionApiService : IApiServiceBase
{
    /// <summary>
    /// 创建会话；若同场景已有活跃会话则可能直接复用。
    /// Creates a session and may reuse an existing active session for the same scene.
    /// </summary>
    Task<ConversationTaskOutput> CreateAsync(CreateSessionInput input, CancellationToken ct = default);

    /// <summary>
    /// 向现有会话追加消息。
    /// Appends a message to an existing session.
    /// </summary>
    Task<ConversationTaskOutput> SendMessageAsync(SendSessionMessageRequest request, CancellationToken ct = default);

    /// <summary>
    /// 获取会话详情及可选的帧信息。
    /// Gets the session details and related frame metadata.
    /// </summary>
    Task<SessionDto?> GetByIdAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// 获取指定项目场景下最近的活跃会话。
    /// Gets the latest active session for a project scene.
    /// </summary>
    Task<SessionDto?> GetActiveBySceneAsync(GetActiveProjectSessionRequest request, CancellationToken ct = default);

    /// <summary>
    /// 获取会话事件流快照。
    /// Gets the current snapshot of session events.
    /// </summary>
    Task<List<SessionEventDto>> GetEventsAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// 获取指定栈帧中的消息列表。
    /// Gets the messages that belong to a specific frame.
    /// </summary>
    Task<List<ChatMessageDto>> GetFrameMessagesAsync(GetFrameMessagesRequest request, CancellationToken ct = default);

    /// <summary>
    /// 取消整个会话。
    /// Cancels the entire session.
    /// </summary>
    Task CancelAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// 弹出当前活动栈帧。
    /// Pops the currently active frame from the session stack.
    /// </summary>
    Task PopFrameAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// 获取项目下的近期会话列表。
    /// Gets recent sessions for a project.
    /// </summary>
    Task<List<SessionDto>> GetByProjectAsync(GetSessionsByProjectRequest request, CancellationToken ct = default);

    /// <summary>
    /// 分页获取对外可见的聊天消息。
    /// Gets externally visible chat messages in pages.
    /// </summary>
    Task<ChatMessageListOutput> GetChatMessagesAsync(GetChatMessagesRequest request, CancellationToken ct = default);
}


