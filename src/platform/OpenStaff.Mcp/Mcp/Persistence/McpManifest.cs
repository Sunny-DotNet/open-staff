using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: `stdio` 安装的核心清单文件模型，也是运行时解析的事实来源。
/// en: Core manifest model for managed installs; it is the source of truth for runtime resolution.
/// </summary>
public sealed class McpManifest
{
    public Guid InstallId { get; init; }

    public string CatalogEntryId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string SourceKey { get; init; } = string.Empty;

    public McpChannelType ChannelType { get; init; }

    public McpTransportType TransportType { get; init; }

    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// zh-CN: 相对于数据根目录的安装目录；保持相对路径便于数据根整体迁移。
    /// en: Install directory relative to the data root so the whole data root can be relocated safely.
    /// </summary>
    public string InstallDirectory { get; init; } = string.Empty;

    public PersistedRuntimeSpec Runtime { get; init; } = new();

    public IReadOnlyList<ManagedArtifact> Artifacts { get; init; } = [];

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}
