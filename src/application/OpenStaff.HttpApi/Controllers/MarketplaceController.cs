
using Microsoft.AspNetCore.Mvc;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// MCP 市场控制器。
/// Controller that exposes MCP marketplace endpoints.
/// </summary>
[ApiController]
[Route("api/mcp/marketplace")]
public class MarketplaceController : ControllerBase
{
    private readonly IMarketplaceApiService _service;

    /// <summary>
    /// 初始化 MCP 市场控制器。
    /// Initializes the MCP marketplace controller.
    /// </summary>
    /// <param name="service">注入的市场应用服务，负责封装市场源查询、搜索与安装逻辑，控制器仅负责 HTTP 端点映射。 / Injected marketplace application service that encapsulates source lookup, search, and installation logic while the controller remains focused on HTTP endpoint mapping.</param>
    public MarketplaceController(IMarketplaceApiService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取所有已注册的市场源。
    /// Gets all registered marketplace sources.
    /// </summary>
    [HttpGet("sources")]
    public Task<List<MarketplaceSourceDto>> GetSources(CancellationToken ct)
        => _service.GetSourcesAsync(ct);

    /// <summary>
    /// 搜索 MCP 市场条目。
    /// Searches MCP marketplace entries.
    /// </summary>
    [HttpGet("search")]
    public Task<MarketplaceSearchResultDto> Search([FromQuery] MarketplaceSearchQueryDto query, CancellationToken ct)
        => _service.SearchAsync(query, ct);

    /// <summary>
    /// 从市场安装 MCP 服务定义。
    /// Installs an MCP server definition from the marketplace.
    /// </summary>
    [HttpPost("install")]
    public Task<MarketplaceServerDto> Install([FromBody] InstallFromMarketplaceInput input, CancellationToken ct)
        => _service.InstallAsync(input, ct);
}

