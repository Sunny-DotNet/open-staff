namespace OpenStaff.Skills.Models;

/// <summary>
/// Paginated skill catalog result.
/// </summary>
public sealed class SkillCatalogSearchResult
{
    /// <summary>
    /// Current page items.
    /// </summary>
    public IReadOnlyList<SkillCatalogEntry> Items { get; init; } = [];

    /// <summary>
    /// Total result count.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 1-based page number.
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Page size.
    /// </summary>
    public int PageSize { get; init; }
}
