namespace OpenStaff.Application.Contracts.Agents.Dtos;

public class AgentDto
{
    public Guid Id { get; set; }
    public string? RoleType { get; set; }
    public string? RoleName { get; set; }
    public string? Status { get; set; }
}

public class PagedAgentEventsDto
{
    public List<AgentEventDto> Items { get; set; } = [];
    public int Total { get; set; }
}

public class AgentEventDto
{
    public Guid Id { get; set; }
    public string? EventType { get; set; }
    public string? Data { get; set; }
    public DateTime CreatedAt { get; set; }
}
