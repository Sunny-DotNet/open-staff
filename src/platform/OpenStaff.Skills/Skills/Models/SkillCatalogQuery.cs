namespace OpenStaff.Skills.Models;

/// <summary>
/// Search query for the skill catalog.
/// </summary>
public sealed class SkillCatalogQuery
{
    /// <summary>
    /// Optional keyword filter.
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>
    /// Optional owner filter.
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// Optional repository filter.
    /// </summary>
    public string? Repo { get; init; }

    /// <summary>
    /// 1-based page number.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Page size.
    /// </summary>
    public int PageSize { get; init; } = 24;
}
