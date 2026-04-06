namespace OpenStaff.Marketplace;

/// <summary>
/// MCP 市场数据源接口 — 每个来源（内置/Registry/第三方）实现此接口
/// </summary>
public interface IMcpMarketplaceSource
{
    /// <summary>源标识（如 "internal"、"registry"）</summary>
    string SourceKey { get; }

    /// <summary>显示名称（如 "内置"、"官方 Registry"）</summary>
    string DisplayName { get; }

    /// <summary>源图标 URL（可选）</summary>
    string? IconUrl { get; }

    /// <summary>搜索 MCP Server</summary>
    Task<MarketplaceSearchResult> SearchAsync(MarketplaceSearchQuery query, CancellationToken ct = default);

    /// <summary>按 ID 获取单个 Server 详情</summary>
    Task<MarketplaceServerInfo?> GetByIdAsync(string serverId, CancellationToken ct = default);
}
