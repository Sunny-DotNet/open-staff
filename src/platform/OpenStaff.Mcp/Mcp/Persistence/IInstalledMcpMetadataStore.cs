using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: 元数据存储抽象，负责安装记录本身。
/// en: Metadata-store abstraction responsible for installation records themselves.
/// </summary>
public interface IInstalledMcpMetadataStore
{
    Task<IReadOnlyList<InstalledMcp>> ListAsync(CancellationToken cancellationToken = default);

    Task<InstalledMcp?> GetAsync(Guid installId, CancellationToken cancellationToken = default);

    Task<InstalledMcp?> GetByCatalogEntryAsync(string sourceKey, string catalogEntryId, CancellationToken cancellationToken = default);

    Task UpsertAsync(InstalledMcp installedMcp, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid installId, CancellationToken cancellationToken = default);
}
