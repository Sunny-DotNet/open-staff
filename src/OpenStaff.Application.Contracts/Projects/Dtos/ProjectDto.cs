namespace OpenStaff.Application.Contracts.Projects.Dtos;

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? WorkspacePath { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateProjectInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateProjectInput
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}
