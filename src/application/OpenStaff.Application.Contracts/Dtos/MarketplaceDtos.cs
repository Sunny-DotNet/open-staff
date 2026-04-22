namespace OpenStaff.Dtos;

/// <summary>
/// 市场源摘要信息。
/// Summary information about a marketplace source.
/// </summary>
public class MarketplaceSourceDto
{
    /// <summary>市场源唯一键。 / Unique marketplace source key.</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>市场源显示名称。 / Display name of the marketplace source.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>市场源图标地址。 / Marketplace source icon URL.</summary>
    public string? IconUrl { get; set; }
}

/// <summary>
/// MCP 市场中的服务定义。
/// MCP server entry returned by the marketplace.
/// </summary>
public class MarketplaceServerDto
{
    /// <summary>市场侧服务标识。 / Marketplace-side server identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>服务名称。 / Server name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>服务描述。 / Server description.</summary>
    public string? Description { get; set; }

    /// <summary>图标或图标数据。 / Icon or icon payload.</summary>
    public string? Icon { get; set; }

    /// <summary>分类名称。 / Category name.</summary>
    public string Category { get; set; } = "general";

    /// <summary>支持的传输协议列表。 / Supported transport types.</summary>
    public List<string> TransportTypes { get; set; } = [];

    /// <summary>来源标识。 / Source identifier.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>版本号。 / Version string.</summary>
    public string? Version { get; set; }

    /// <summary>代码仓库地址。 / Repository URL.</summary>
    public string? RepositoryUrl { get; set; }

    /// <summary>主页地址。 / Homepage URL.</summary>
    public string? Homepage { get; set; }

    /// <summary>NPM 包名称。 / NPM package name.</summary>
    public string? NpmPackage { get; set; }

    /// <summary>PyPI 包名称。 / PyPI package name.</summary>
    public string? PypiPackage { get; set; }

    /// <summary>默认配置模板。 / Default configuration template.</summary>
    public string? DefaultConfig { get; set; }

    /// <summary>是否已安装到本地目录。 / Whether the server has already been installed locally.</summary>
    public bool IsInstalled { get; set; }
}

/// <summary>
/// 市场搜索查询参数。
/// Query parameters used when searching marketplace entries.
/// </summary>
public class MarketplaceSearchQueryDto
{
    /// <summary>限定搜索的市场源键。 / Optional source key to scope the search.</summary>
    public string? SourceKey { get; set; }

    /// <summary>关键字搜索词。 / Keyword search term.</summary>
    public string? Keyword { get; set; }

    /// <summary>分类过滤条件。 / Category filter.</summary>
    public string? Category { get; set; }

    /// <summary>游标式分页游标。 / Cursor used for cursor-based paging.</summary>
    public string? Cursor { get; set; }

    /// <summary>页码。 / Page number.</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页数量。 / Number of items per page.</summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// 市场搜索结果。
/// Search result returned by the marketplace.
/// </summary>
public class MarketplaceSearchResultDto
{
    /// <summary>当前页项目。 / Items returned for the current search.</summary>
    public List<MarketplaceServerDto> Items { get; set; } = [];

    /// <summary>总命中数量。 / Total number of matching entries.</summary>
    public int TotalCount { get; set; }

    /// <summary>下一页游标。 / Cursor for the next page.</summary>
    public string? NextCursor { get; set; }
}

/// <summary>
/// 从市场安装 MCP 服务的输入参数。
/// Input used to install an MCP server from the marketplace.
/// </summary>
public class InstallFromMarketplaceInput
{
    /// <summary>市场源键。 / Marketplace source key.</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>市场侧服务标识。 / Marketplace-side server identifier.</summary>
    public string ServerId { get; set; } = string.Empty;

    /// <summary>可选的本地覆盖名称。 / Optional local override name.</summary>
    public string? Name { get; set; }
}
