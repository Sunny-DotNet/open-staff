namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示本地已安装的 MCP 聚合根。
/// en: Represents a locally installed MCP aggregate.
/// </summary>
public sealed class InstalledMcp
{
    public Guid InstallId { get; set; }

    public string CatalogEntryId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string SourceKey { get; set; } = string.Empty;

    public McpChannelType ChannelType { get; set; }

    public McpTransportType TransportType { get; set; }

    public string Version { get; set; } = string.Empty;

    public InstallState InstallState { get; set; }

    public string ManifestPath { get; set; } = string.Empty;

    public string InstallDirectory { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? LastError { get; set; }
}
