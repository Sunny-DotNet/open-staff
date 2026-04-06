using Microsoft.AspNetCore.Mvc;
using OpenStaff.Application.Contracts.Marketplace;
using OpenStaff.Application.Contracts.Marketplace.Dtos;

namespace OpenStaff.HttpApi.Controllers;

[ApiController]
[Route("api/mcp/marketplace")]
public class MarketplaceController : ControllerBase
{
    private readonly IMarketplaceAppService _service;

    public MarketplaceController(IMarketplaceAppService service)
    {
        _service = service;
    }

    /// <summary>获取所有已注册的市场数据源</summary>
    [HttpGet("sources")]
    public Task<List<MarketplaceSourceDto>> GetSources(CancellationToken ct)
        => _service.GetSourcesAsync(ct);

    /// <summary>搜索 MCP Server</summary>
    [HttpGet("search")]
    public Task<MarketplaceSearchResultDto> Search(
        [FromQuery] MarketplaceSearchQueryDto query, CancellationToken ct)
        => _service.SearchAsync(query, ct);

    /// <summary>从外部源安装 MCP Server 到本地</summary>
    [HttpPost("install")]
    public Task<MarketplaceServerDto> Install(
        [FromBody] InstallFromMarketplaceInput input, CancellationToken ct)
        => _service.InstallAsync(input, ct);
}
