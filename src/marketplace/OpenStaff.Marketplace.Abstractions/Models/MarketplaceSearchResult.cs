namespace OpenStaff.Marketplace;

/// <summary>
/// 市场搜索查询参数
/// </summary>
public class MarketplaceSearchQuery
{
    /// <summary>关键词搜索</summary>
    public string? Keyword { get; set; }

    /// <summary>分类筛选</summary>
    public string? Category { get; set; }

    /// <summary>分页游标（用于 Registry 等游标分页 API）</summary>
    public string? Cursor { get; set; }

    /// <summary>页码（用于传统分页）</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页数量</summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// 市场搜索结果（支持游标 + 传统分页）
/// </summary>
public class MarketplaceSearchResult
{
    public List<MarketplaceServerInfo> Items { get; set; } = [];
    public int TotalCount { get; set; }

    /// <summary>下一页游标（null 表示无更多数据）</summary>
    public string? NextCursor { get; set; }

    public bool HasMore => NextCursor != null || Items.Count >= TotalCount == false;
}
