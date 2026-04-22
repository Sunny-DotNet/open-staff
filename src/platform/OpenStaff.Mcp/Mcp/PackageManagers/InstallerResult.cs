using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Persistence;

namespace OpenStaff.Mcp.PackageManagers;

/// <summary>
/// zh-CN: 单个安装器的执行结果。
/// en: Result produced by a single installer.
/// </summary>
public sealed class InstallerResult
{
    public required string InstalledVersion { get; init; }

    public required PersistedRuntimeSpec Runtime { get; init; }

    public IReadOnlyList<ManagedArtifact> Artifacts { get; init; } = [];
}
