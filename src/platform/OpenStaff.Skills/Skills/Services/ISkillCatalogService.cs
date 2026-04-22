using OpenStaff.Skills.Models;

namespace OpenStaff.Skills.Services;

/// <summary>
/// Public service for browsing the skill catalog.
/// </summary>
public interface ISkillCatalogService
{
    /// <summary>
    /// Searches the catalog.
    /// </summary>
    Task<SkillCatalogSearchResult> SearchAsync(SkillCatalogQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single catalog entry.
    /// </summary>
    Task<SkillCatalogEntry?> GetAsync(string owner, string repo, string skillId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the available catalog sources.
    /// </summary>
    Task<IReadOnlyList<SkillCatalogSource>> GetSourcesAsync(CancellationToken cancellationToken = default);
}
