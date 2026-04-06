using OpenStaff.Application.Contracts.Marketplace.Dtos;

namespace OpenStaff.Application.Contracts.Marketplace;

public interface IMarketplaceAppService
{
    /// <summary>获取所有已注册的市场源</summary>
    Task<List<MarketplaceSourceDto>> GetSourcesAsync(CancellationToken ct = default);

    /// <summary>按源搜索 MCP Server</summary>
    Task<MarketplaceSearchResultDto> SearchAsync(MarketplaceSearchQueryDto query, CancellationToken ct = default);

    /// <summary>从外部源安装 MCP Server 到本地</summary>
    Task<MarketplaceServerDto> InstallAsync(InstallFromMarketplaceInput input, CancellationToken ct = default);
}
