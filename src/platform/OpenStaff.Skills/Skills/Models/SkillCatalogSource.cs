namespace OpenStaff.Skills.Models;

/// <summary>
/// Describes a skill catalog source.
/// </summary>
public sealed class SkillCatalogSource
{
    /// <summary>
    /// Stable source key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Human-readable source name.
    /// </summary>
    public required string DisplayName { get; init; }
}
