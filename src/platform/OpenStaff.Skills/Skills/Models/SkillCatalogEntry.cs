namespace OpenStaff.Skills.Models;

/// <summary>
/// Represents a catalog skill entry.
/// </summary>
public sealed class SkillCatalogEntry
{
    /// <summary>
    /// Stable source key.
    /// </summary>
    public required string SourceKey { get; init; }

    /// <summary>
    /// Repository owner.
    /// </summary>
    public required string Owner { get; init; }

    /// <summary>
    /// Repository name.
    /// </summary>
    public required string Repo { get; init; }

    /// <summary>
    /// Skill identifier.
    /// </summary>
    public required string SkillId { get; init; }

    /// <summary>
    /// Canonical skill name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// User-facing skill name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Skill description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Repository URL.
    /// </summary>
    public string? RepositoryUrl { get; init; }

    /// <summary>
    /// Snapshot of install count from the source.
    /// </summary>
    public int Installs { get; init; }
}
