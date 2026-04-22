using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: 已安装 MCP 查询接口。
/// en: Query contract for installed MCP records.
/// </summary>
public interface IInstalledMcpService
{
    Task<IReadOnlyList<InstalledMcp>> ListInstalledAsync(CancellationToken cancellationToken = default);

    Task<InstalledMcp?> GetInstalledAsync(Guid installId, CancellationToken cancellationToken = default);
}
