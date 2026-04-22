namespace OpenStaff.Marketplace;

/// <summary>
/// 统一的 MCP Server 元信息模型，屏蔽不同市场源之间的结构差异。
/// Unified MCP server metadata model that hides structural differences between marketplace sources.
/// </summary>
public class MarketplaceServerInfo
{
    /// <summary>
    /// 源内唯一标识。
    /// Source-specific unique identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 服务器名称。
    /// Server name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 服务器说明。
    /// Server description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 服务器图标。
    /// Server icon.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 服务器分类。
    /// Server category.
    /// </summary>
    public string Category { get; set; } = "general";

    /// <summary>
    /// 支持的传输类型列表。
    /// Supported transport types.
    /// </summary>
    public List<string> TransportTypes { get; set; } = [];

    /// <summary>
    /// 数据来源标识。
    /// Data source identifier.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 当前版本号。
    /// Current version number.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// 仓库地址。
    /// Repository URL.
    /// </summary>
    public string? RepositoryUrl { get; set; }

    /// <summary>
    /// 主页地址。
    /// Homepage URL.
    /// </summary>
    public string? Homepage { get; set; }

    /// <summary>
    /// 对应的 npm 包名称。
    /// Associated npm package name.
    /// </summary>
    public string? NpmPackage { get; set; }

    /// <summary>
    /// 对应的 PyPI 包名称。
    /// Associated PyPI package name.
    /// </summary>
    public string? PypiPackage { get; set; }

    /// <summary>
    /// 远程端点列表。
    /// List of remote endpoints.
    /// </summary>
    public List<RemoteEndpoint> Remotes { get; set; } = [];

    /// <summary>
    /// 默认配置模板 JSON。
    /// Default configuration template JSON.
    /// </summary>
    public string? DefaultConfig { get; set; }

    /// <summary>
    /// 是否已安装到本地。
    /// Indicates whether the server is already installed locally.
    /// </summary>
    public bool IsInstalled { get; set; }
}

/// <summary>
/// 远程传输端点描述。
/// Description of a remote transport endpoint.
/// </summary>
public class RemoteEndpoint
{
    /// <summary>
    /// 传输类型，例如 <c>streamable-http</c> 或 <c>sse</c>。
    /// Transport type such as <c>streamable-http</c> or <c>sse</c>.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 端点地址。
    /// Endpoint URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;
}
