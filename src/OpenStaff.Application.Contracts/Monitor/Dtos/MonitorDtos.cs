namespace OpenStaff.Application.Contracts.Monitor.Dtos;

public class SystemStatsDto
{
    public int Projects { get; set; }
    public int Agents { get; set; }
    public int Tasks { get; set; }
    public int Events { get; set; }
    public int CompletedTasks { get; set; }
    public int Sessions { get; set; }
    public int ModelProviders { get; set; }
    public int AgentRoles { get; set; }
    public List<RecentSessionDto> RecentSessions { get; set; } = [];
}

public class RecentSessionDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string? Status { get; set; }
    public string? Input { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProjectStatsDto
{
    public List<ProjectAgentDto> Agents { get; set; } = [];
    public Dictionary<string, int> TasksByStatus { get; set; } = [];
    public List<EventDto> RecentEvents { get; set; } = [];
    public int Checkpoints { get; set; }
}

public class ProjectAgentDto
{
    public Guid Id { get; set; }
    public string? RoleName { get; set; }
    public string? RoleType { get; set; }
    public string? Status { get; set; }
}

public class EventDto
{
    public Guid Id { get; set; }
    public string? EventType { get; set; }
    public string? Data { get; set; }
    public Guid? AgentId { get; set; }
    public string? AgentName { get; set; }
    public DateTime CreatedAt { get; set; }
}
