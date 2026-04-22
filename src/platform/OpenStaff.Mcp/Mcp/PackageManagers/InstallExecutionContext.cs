using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.PackageManagers;

/// <summary>
/// zh-CN: 安装器执行上下文，封装安装目标、状态回调与最终安装目录。
/// en: Execution context passed to installers, encapsulating the install target, status callback, and final install directory.
/// </summary>
public sealed class InstallExecutionContext
{
    public required Guid InstallId { get; init; }

    public required InstallRequest Request { get; init; }

    public required CatalogEntry CatalogEntry { get; init; }

    public required InstallChannel Channel { get; init; }

    public required string InstallDirectory { get; init; }

    public required Func<InstallState, string?, Task> UpdateStateAsync { get; init; }
}
