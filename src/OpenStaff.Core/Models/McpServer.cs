namespace OpenStaff.Core.Models;

/// <summary>
/// MCP 服务定义 — 描述一个 MCP Server 的元信息（市场展示用）
/// </summary>
public class McpServer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; } // 图标 URL 或内置图标名
    public string Category { get; set; } = "general"; // 分类: dev-tools, search, filesystem, database, ...
    public string TransportType { get; set; } = "stdio"; // stdio / http
    public string Source { get; set; } = "builtin"; // builtin / custom / marketplace
    public string? DefaultConfig { get; set; } // JSON: 默认命令/URL/参数模板
    public string? MarketplaceUrl { get; set; } // 外部市场来源 URL
    public string? Homepage { get; set; } // 项目主页
    public string? NpmPackage { get; set; } // npm 包名（stdio 类型常用）
    public string? PypiPackage { get; set; } // pypi 包名
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    public ICollection<McpServerConfig> Configs { get; set; } = new List<McpServerConfig>();
}

/// <summary>MCP 服务分类常量</summary>
public static class McpCategories
{
    public const string General = "general";
    public const string DevTools = "dev-tools";
    public const string Search = "search";
    public const string Filesystem = "filesystem";
    public const string Database = "database";
    public const string Browser = "browser";
    public const string Memory = "memory";
    public const string Communication = "communication";
}

/// <summary>MCP 传输类型常量</summary>
public static class McpTransportTypes
{
    public const string Stdio = "stdio";
    public const string Http = "http";
}

/// <summary>MCP 数据来源常量</summary>
public static class McpSources
{
    public const string Builtin = "builtin";
    public const string Custom = "custom";
    public const string Marketplace = "marketplace";
}
