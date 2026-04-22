using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.PackageManagers;

/// <summary>
/// zh-CN: 安装通道执行器接口。
/// en: Contract for install-channel executors.
/// </summary>
public interface IInstallChannelInstaller
{
    IReadOnlyCollection<McpChannelType> SupportedChannelTypes { get; }

    Task<InstallerResult> InstallAsync(InstallExecutionContext context, CancellationToken cancellationToken = default);
}
