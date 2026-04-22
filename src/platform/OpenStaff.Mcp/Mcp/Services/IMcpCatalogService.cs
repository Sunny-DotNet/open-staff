using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: 目录搜索用例接口。
/// en: Use-case contract for catalog search.
/// </summary>
public interface IMcpCatalogService
{
    Task<CatalogSearchResult> SearchCatalogAsync(CatalogSearchQuery query, CancellationToken cancellationToken = default);

    Task<CatalogEntry> GetCatalogEntryAsync(string sourceKey, string entryId, CancellationToken cancellationToken = default);
}
