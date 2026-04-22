namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示目录搜索输入。
/// en: Represents the input for catalog search.
/// </summary>
public sealed class CatalogSearchQuery
{
    public string? Keyword { get; init; }

    public string? Category { get; init; }

    public string? SourceKey { get; init; }

    public McpTransportType? TransportType { get; init; }

    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public string? Cursor { get; init; }

    /// <summary>
    /// zh-CN: 聚合搜索时会先拉取各来源的完整候选集，再统一分页，因此这里提供一个去分页副本。
    /// en: Aggregated search first pulls the full candidate set from each source and then applies unified paging, so this helper produces an unpaged copy.
    /// </summary>
    public CatalogSearchQuery ToUnpagedQuery()
    {
        return new CatalogSearchQuery
        {
            Keyword = Keyword,
            Category = Category,
            SourceKey = SourceKey,
            TransportType = TransportType
        };
    }
}
