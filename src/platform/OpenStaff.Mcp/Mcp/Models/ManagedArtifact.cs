namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示安装过程中产生的受管产物。
/// en: Represents a managed artifact produced during installation.
/// </summary>
public sealed class ManagedArtifact
{
    public ManagedArtifactType ArtifactType { get; init; }

    public string RelativePath { get; init; } = string.Empty;

    public string? Checksum { get; init; }

    public long? Size { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
