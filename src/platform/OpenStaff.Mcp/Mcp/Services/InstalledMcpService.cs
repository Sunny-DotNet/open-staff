using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Persistence;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: 已安装 MCP 查询服务。
/// en: Installed MCP query service.
/// </summary>
public sealed class InstalledMcpService : IInstalledMcpService
{
    private readonly IInstalledMcpMetadataStore _metadataStore;

    public InstalledMcpService(IInstalledMcpMetadataStore metadataStore)
    {
        _metadataStore = metadataStore;
    }

    public Task<IReadOnlyList<InstalledMcp>> ListInstalledAsync(CancellationToken cancellationToken = default)
        => _metadataStore.ListAsync(cancellationToken);

    public Task<InstalledMcp?> GetInstalledAsync(Guid installId, CancellationToken cancellationToken = default)
        => _metadataStore.GetAsync(installId, cancellationToken);
}
