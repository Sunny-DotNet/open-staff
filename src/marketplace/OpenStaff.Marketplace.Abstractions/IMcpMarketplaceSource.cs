namespace OpenStaff.Marketplace;

/// <summary>
/// MCP 市场源接口，每个市场来源都通过该接口暴露统一的搜索与详情读取能力。
/// MCP marketplace source contract that gives each marketplace provider a uniform search and detail lookup surface.
/// </summary>
public interface IMcpMarketplaceSource
{
    /// <summary>
    /// 市场源唯一键，例如 <c>internal</c> 或 <c>registry</c>。
    /// Unique marketplace source key such as <c>internal</c> or <c>registry</c>.
    /// </summary>
    string SourceKey { get; }

    /// <summary>
    /// 市场源显示名称。
    /// Human-readable marketplace source name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 市场源图标地址。
    /// Marketplace source icon URL.
    /// </summary>
    string? IconUrl { get; }

    /// <summary>
    /// 搜索 MCP Server。
    /// Searches MCP servers from the current source.
    /// </summary>
    /// <param name="query">
    /// 搜索查询参数。
    /// Search query parameters.
    /// </param>
    /// <param name="ct">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// 搜索结果。
    /// Search result.
    /// </returns>
    Task<MarketplaceSearchResult> SearchAsync(MarketplaceSearchQuery query, CancellationToken ct = default);

    /// <summary>
    /// 按标识获取单个 MCP Server 详情。
    /// Gets details for a single MCP server by identifier.
    /// </summary>
    /// <param name="serverId">
    /// 服务器标识。
    /// Server identifier.
    /// </param>
    /// <param name="ct">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// 服务器详情；未找到时返回 <see langword="null" />。
    /// Server details, or <see langword="null" /> when not found.
    /// </returns>
    Task<MarketplaceServerInfo?> GetByIdAsync(string serverId, CancellationToken ct = default);
}
