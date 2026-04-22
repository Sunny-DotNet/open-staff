namespace OpenStaff.Entities;

/// <summary>
/// MCP 服务定义 — 描述一个 MCP Server 的元信息（市场展示用）
/// </summary>
public class McpServer:EntityBase<Guid>
{
    /// <summary>展示名称 / Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>展示描述 / Optional description for the catalog.</summary>
    public string? Description { get; set; }

    /// <summary>图标 URL 或内置图标名 / Icon URL or built-in icon name.</summary>
    public string? Icon { get; set; }

    /// <summary>服务分类 / Catalog category such as dev-tools or filesystem.</summary>
    public string Category { get; set; } = "general";

    /// <summary>传输类型 / Transport type, such as stdio or http.</summary>
    public string TransportType { get; set; } = "stdio";

    /// <summary>安装模式 / Install mode, such as local or remote.</summary>
    public string Mode { get; set; } = McpServerModes.Local;

    /// <summary>数据来源 / Source channel, such as builtin, custom, or marketplace.</summary>
    public string Source { get; set; } = "builtin";

    /// <summary>默认配置模板 JSON / Default configuration template stored as JSON.</summary>
    public string? DefaultConfig { get; set; }

    /// <summary>安装信息 JSON / Install or endpoint metadata stored as JSON.</summary>
    public string? InstallInfo { get; set; }

    /// <summary>外部市场来源 URL / External marketplace URL.</summary>
    public string? MarketplaceUrl { get; set; }

    /// <summary>项目主页 / Project homepage.</summary>
    public string? Homepage { get; set; }

    /// <summary>关联的 npm 包名 / Related npm package name.</summary>
    public string? NpmPackage { get; set; }

    /// <summary>关联的 PyPI 包名 / Related PyPI package name.</summary>
    public string? PypiPackage { get; set; }

    /// <summary>是否启用 / Whether the server definition is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间（UTC） / Last update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>角色测试场景对当前 server 的使用绑定。 / Role-test bindings that use this server.</summary>
    public ICollection<AgentRoleMcpBinding> RoleBindings { get; set; } = new List<AgentRoleMcpBinding>();
}

/// <summary>MCP 服务分类常量 / Well-known MCP server categories.</summary>
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

/// <summary>MCP 传输类型常量 / Supported MCP transport types.</summary>
public static class McpTransportTypes
{
    public const string Stdio = "stdio";
    public const string Builtin = "builtin";
    public const string Http = "http";
    public const string Sse = "sse";
    public const string StreamableHttp = "streamable-http";
}

/// <summary>MCP 数据来源常量 / Supported MCP definition sources.</summary>
public static class McpSources
{
    public const string Builtin = "builtin";
    public const string Custom = "custom";
    public const string Marketplace = "marketplace";
}

/// <summary>MCP 安装模式常量 / Supported MCP server installation modes.</summary>
public static class McpServerModes
{
    public const string Local = "local";
    public const string Remote = "remote";
}
