using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// MCP 市场应用服务契约。
/// Application service contract for browsing and installing MCP marketplace entries.
/// </summary>
public interface IMarketplaceApiService : IApiServiceBase
{
    /// <summary>
    /// 获取所有已注册的市场源。
    /// Gets all registered marketplace sources.
    /// </summary>
    Task<List<MarketplaceSourceDto>> GetSourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// 按查询条件搜索 MCP Server。
    /// Searches marketplace MCP servers using the provided query.
    /// </summary>
    Task<MarketplaceSearchResultDto> SearchAsync(MarketplaceSearchQueryDto query, CancellationToken ct = default);

    /// <summary>
    /// 从外部市场源安装 MCP Server 到本地。
    /// Installs an MCP server definition from an external marketplace source.
    /// </summary>
    Task<MarketplaceServerDto> InstallAsync(InstallFromMarketplaceInput input, CancellationToken ct = default);
}


