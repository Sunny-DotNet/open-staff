namespace OpenStaff.Application.Contracts.Sessions.Dtos;

public class SessionDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string? Status { get; set; }
    public string? Input { get; set; }
    public string? Result { get; set; }
    public string? ContextStrategy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsActive { get; set; }
    public List<SessionFrameDto>? Frames { get; set; }
}

public class SessionFrameDto
{
    public Guid Id { get; set; }
    public string? AgentRole { get; set; }
    public string? Status { get; set; }
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SessionEventDto
{
    public Guid Id { get; set; }
    public string? EventType { get; set; }
    public string? Data { get; set; }
    public int SequenceNo { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChatMessageDto
{
    public string? Role { get; set; }
    public string? AgentRole { get; set; }
    public string? Content { get; set; }
    public string? ContentType { get; set; }
    public int? TokenUsage { get; set; }
    public long? DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSessionInput
{
    public Guid ProjectId { get; set; }
    public string Input { get; set; } = string.Empty;
    public string? ContextStrategy { get; set; }
}

public class CreateSessionOutput
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class SendMessageOutput
{
    public string Status { get; set; } = string.Empty;
    public bool IsAwaitingInput { get; set; }
}

public class ChatMessageListOutput
{
    public List<ChatMessageDto> Messages { get; set; } = [];
    public int Total { get; set; }
}

public class SendSessionMessageRequest
{
    public Guid SessionId { get; set; }
    public string Input { get; set; } = string.Empty;
}

public class GetChatMessagesRequest
{
    public Guid SessionId { get; set; }
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 50;
}

public class GetSessionsByProjectRequest
{
    public Guid ProjectId { get; set; }
    public int Limit { get; set; } = 20;
}

public class GetFrameMessagesRequest
{
    public Guid SessionId { get; set; }
    public Guid FrameId { get; set; }
}
