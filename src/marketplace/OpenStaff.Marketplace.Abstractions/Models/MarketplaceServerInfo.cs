namespace OpenStaff.Marketplace;

/// <summary>
/// 统一的 MCP Server 元信息（跨源通用）
/// </summary>
public class MarketplaceServerInfo
{
    /// <summary>源内唯一标识（Internal 用 Guid，Registry 用 name:version）</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string Category { get; set; } = "general";

    /// <summary>支持的传输类型: stdio / http / streamable-http</summary>
    public List<string> TransportTypes { get; set; } = [];

    /// <summary>数据来源标识</summary>
    public string Source { get; set; } = string.Empty;

    public string? Version { get; set; }
    public string? RepositoryUrl { get; set; }
    public string? Homepage { get; set; }
    public string? NpmPackage { get; set; }
    public string? PypiPackage { get; set; }

    /// <summary>远程端点列表（HTTP 类型）</summary>
    public List<RemoteEndpoint> Remotes { get; set; } = [];

    /// <summary>默认配置模板 JSON</summary>
    public string? DefaultConfig { get; set; }

    /// <summary>是否已安装到本地</summary>
    public bool IsInstalled { get; set; }
}

public class RemoteEndpoint
{
    public string Type { get; set; } = string.Empty; // "streamable-http", "sse"
    public string Url { get; set; } = string.Empty;
}
