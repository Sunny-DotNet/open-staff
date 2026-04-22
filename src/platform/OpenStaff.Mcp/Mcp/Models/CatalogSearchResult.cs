namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示目录搜索结果。
/// en: Represents the result of a catalog search request.
/// </summary>
public sealed class CatalogSearchResult
{
    public IReadOnlyList<CatalogEntry> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public string? NextCursor { get; init; }
}
