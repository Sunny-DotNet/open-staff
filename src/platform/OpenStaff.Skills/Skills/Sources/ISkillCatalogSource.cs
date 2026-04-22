using OpenStaff.Skills.Models;

namespace OpenStaff.Skills.Sources;

/// <summary>
/// Catalog source for skill metadata.
/// </summary>
public interface ISkillCatalogSource
{
    /// <summary>
    /// Stable source key.
    /// </summary>
    string SourceKey { get; }

    /// <summary>
    /// Human-readable source name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets all catalog entries from this source.
    /// </summary>
    Task<IReadOnlyList<SkillCatalogEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one catalog entry, including best-effort detail enrichment.
    /// </summary>
    Task<SkillCatalogEntry?> GetAsync(string owner, string repo, string skillId, CancellationToken cancellationToken = default);
}
