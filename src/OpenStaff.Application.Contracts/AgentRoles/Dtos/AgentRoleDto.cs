namespace OpenStaff.Application.Contracts.AgentRoles.Dtos;

public class AgentRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RoleType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public bool IsBuiltin { get; set; }
    public string? ModelProviderId { get; set; }
    public string? ModelProviderName { get; set; }
    public string? ModelName { get; set; }
    public string? Config { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAgentRoleInput
{
    public string Name { get; set; } = string.Empty;
    public string RoleType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public string? ModelProviderId { get; set; }
    public string? ModelName { get; set; }
    public string? Config { get; set; }
}

public class UpdateAgentRoleInput
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public string? ModelProviderId { get; set; }
    public string? ModelName { get; set; }
    public string? Config { get; set; }
}
