namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示搜索结果中的一个 MCP 条目。
/// en: Represents a single MCP entry returned by catalog search.
/// </summary>
public sealed class CatalogEntry
{
    public string EntryId { get; init; } = string.Empty;

    public string SourceKey { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Category { get; init; }

    public string? Version { get; init; }

    public string? Homepage { get; init; }

    public string? RepositoryUrl { get; init; }

    public IReadOnlyList<McpTransportType> TransportTypes { get; init; } = [];

    public IReadOnlyList<InstallChannel> InstallChannels { get; init; } = [];

    public bool IsInstalled { get; init; }

    public InstallState? InstalledState { get; init; }

    public string? InstalledVersion { get; init; }
}
