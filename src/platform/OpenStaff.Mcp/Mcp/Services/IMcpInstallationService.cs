using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: MCP 安装用例接口。
/// en: Use-case contract for MCP installation.
/// </summary>
public interface IMcpInstallationService
{
    Task<InstalledMcp> InstallAsync(InstallRequest request, CancellationToken cancellationToken = default);
}
