namespace OpenStaff.Dtos;

/// <summary>
/// MCP 目录源摘要。
/// Summary information for an MCP catalog source.
/// </summary>
public class McpSourceDto
{
    public string SourceKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// MCP 目录安装通道。
/// MCP install channel returned by catalog APIs.
/// </summary>
public class McpInstallChannelDto
{
    public string ChannelId { get; set; } = string.Empty;

    public string ChannelType { get; set; } = string.Empty;

    public string TransportType { get; set; } = string.Empty;

    public string? Version { get; set; }

    public string? EntrypointHint { get; set; }

    public string? PackageIdentifier { get; set; }

    public string? ArtifactUrl { get; set; }

    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// MCP 目录条目。
/// MCP catalog entry DTO.
/// </summary>
public class McpCatalogEntryDto
{
    public string EntryId { get; set; } = string.Empty;

    public string SourceKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Category { get; set; }

    public string? Version { get; set; }

    public string? Homepage { get; set; }

    public string? RepositoryUrl { get; set; }

    public List<string> TransportTypes { get; set; } = [];

    public List<McpInstallChannelDto> InstallChannels { get; set; } = [];

    public bool IsInstalled { get; set; }

    public string? InstalledState { get; set; }

    public string? InstalledVersion { get; set; }

    public Guid? InstallId { get; set; }
}

/// <summary>
/// MCP 目录搜索输入。
/// MCP catalog search query.
/// </summary>
public class McpCatalogSearchQueryDto
{
    public string? SourceKey { get; set; }

    public string? Keyword { get; set; }

    public string? Category { get; set; }

    public string? TransportType { get; set; }

    public string? Cursor { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>
/// MCP 目录搜索结果。
/// MCP catalog search result.
/// </summary>
public class McpCatalogSearchResultDto
{
    public List<McpCatalogEntryDto> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public string? NextCursor { get; set; }
}

/// <summary>
/// MCP 安装输入。
/// Input used to install an MCP server from the catalog.
/// </summary>
public class InstallMcpServerInput
{
    public string SourceKey { get; set; } = string.Empty;

    public string CatalogEntryId { get; set; } = string.Empty;

    public string? SelectedChannelId { get; set; }

    public string? RequestedVersion { get; set; }

    public string? Name { get; set; }

    public bool OverwriteExisting { get; set; }
}

/// <summary>
/// MCP 删除/卸载检查结果。
/// Result of checking whether an MCP server can be deleted or uninstalled.
/// </summary>
public class McpUninstallCheckResultDto
{
    public bool CanUninstall { get; set; }

    public List<string> BlockingReasons { get; set; } = [];

    public List<string> ReferencedByConfigs { get; set; } = [];

    public List<string> ReferencedByProjectBindings { get; set; } = [];

    public List<string> ReferencedByRoleBindings { get; set; } = [];
}

/// <summary>
/// MCP 删除/卸载结果。
/// Result of deleting or uninstalling an MCP server.
/// </summary>
public class DeleteMcpServerResultDto
{
    public Guid ServerId { get; set; }

    public Guid? InstallId { get; set; }

    public bool Deleted { get; set; }

    public bool Uninstalled { get; set; }

    public string Action { get; set; } = "blocked";

    public string? Message { get; set; }

    public List<string> BlockingReasons { get; set; } = [];

    public List<string> ReferencedByConfigs { get; set; } = [];

    public List<string> ReferencedByProjectBindings { get; set; } = [];

    public List<string> ReferencedByRoleBindings { get; set; } = [];
}

/// <summary>
/// MCP 修复结果。
/// Result of repairing an MCP installation.
/// </summary>
public class McpRepairResultDto
{
    public bool Repaired { get; set; }

    public string? Message { get; set; }

    public McpServerDto Server { get; set; } = new();
}

/// <summary>
/// MCP 草稿配置测试输入。
/// Draft MCP connection-test input.
/// </summary>
public class TestMcpConnectionDraftInput
{
    public Guid McpServerId { get; set; }

    public string? SelectedProfileId { get; set; }

    public string? ParameterValues { get; set; }
}
