namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示一个 MCP 条目的可选安装通道。
/// en: Represents an install channel available for an MCP catalog entry.
/// </summary>
public sealed class InstallChannel
{
    /// <summary>
    /// zh-CN: 通道标识；同一条目内必须唯一。
    /// en: Channel identifier that must be unique within the owning entry.
    /// </summary>
    public string ChannelId { get; init; } = string.Empty;

    public McpChannelType ChannelType { get; init; }

    public McpTransportType TransportType { get; init; }

    public string? Version { get; init; }

    public string? EntrypointHint { get; init; }

    public string? PackageIdentifier { get; init; }

    public string? ArtifactUrl { get; init; }

    public string? Checksum { get; init; }

    /// <summary>
    /// zh-CN: 由来源提供的扩展元数据；安装器会消费约定键，而不会把来源细节泄漏到宿主。
    /// en: Source-provided extension metadata consumed by installers without leaking source-specific details to the host.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
