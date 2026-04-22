using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: MCP 卸载接口。
/// en: Contract for uninstall pre-check and uninstall execution.
/// </summary>
public interface IMcpUninstallService
{
    Task<UninstallCheckResult> CheckUninstallAsync(Guid installId, CancellationToken cancellationToken = default);

    Task UninstallAsync(Guid installId, CancellationToken cancellationToken = default);
}
