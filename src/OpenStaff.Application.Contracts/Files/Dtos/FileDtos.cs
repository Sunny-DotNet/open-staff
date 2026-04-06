namespace OpenStaff.Application.Contracts.Files.Dtos;

public class FileNodeDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public List<FileNodeDto>? Children { get; set; }
}

public class CheckpointDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? CommitSha { get; set; }
    public DateTime CreatedAt { get; set; }
}
