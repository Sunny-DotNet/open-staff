namespace OpenStaff.Marketplace;

/// <summary>
/// 市场搜索查询参数。
/// Marketplace search query parameters.
/// </summary>
public class MarketplaceSearchQuery
{
    /// <summary>
    /// 关键词搜索。
    /// Keyword search text.
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// 分类筛选条件。
    /// Category filter.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 游标分页标记。
    /// Cursor used for cursor-based pagination.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// 传统分页页码。
    /// Page number for offset-based pagination.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每页条目数。
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// 市场搜索结果，兼容游标分页与传统分页。
/// Marketplace search result that supports both cursor-based and offset-based pagination.
/// </summary>
public class MarketplaceSearchResult
{
    /// <summary>
    /// 当前页结果项。
    /// Result items for the current page.
    /// </summary>
    public List<MarketplaceServerInfo> Items { get; set; } = [];

    /// <summary>
    /// 总结果数量。
    /// Total number of results.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 下一页游标；为 <see langword="null" /> 时表示没有更多游标页。
    /// Cursor for the next page; <see langword="null" /> means there is no next cursor page.
    /// </summary>
    public string? NextCursor { get; set; }

    /// <summary>
    /// 指示是否可能还有更多结果。
    /// Indicates whether more results may be available.
    /// </summary>
    public bool HasMore => NextCursor != null || Items.Count >= TotalCount == false;
}
