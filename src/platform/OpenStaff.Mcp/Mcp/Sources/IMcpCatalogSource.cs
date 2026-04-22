using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Sources;

/// <summary>
/// zh-CN: MCP 搜索来源接口；宿主可以注册多个来源，由模块统一聚合。
/// en: MCP catalog source contract; hosts can register multiple sources that the module then aggregates.
/// </summary>
public interface IMcpCatalogSource
{
    string SourceKey { get; }

    string DisplayName { get; }

    int Priority { get; }

    Task<IReadOnlyList<CatalogEntry>> SearchAsync(CatalogSearchQuery query, CancellationToken cancellationToken = default);

    Task<CatalogEntry?> GetByIdAsync(string entryId, CancellationToken cancellationToken = default);
}
