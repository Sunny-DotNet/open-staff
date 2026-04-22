using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Persistence;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: 提供轻量级修复能力：校验 manifest 与安装目录是否仍然存在，并尝试恢复 Ready 状态。
/// en: Provides lightweight repair capabilities by validating that manifests and install directories still exist and trying to restore the Ready state.
/// </summary>
public sealed class McpRepairService : IMcpRepairService
{
    private readonly IInstalledMcpMetadataStore _metadataStore;
    private readonly IMcpManifestStore _manifestStore;
    private readonly IMcpRuntimeResolver _runtimeResolver;
    private readonly IMcpDataDirectoryLayout _layout;

    public McpRepairService(
        IInstalledMcpMetadataStore metadataStore,
        IMcpManifestStore manifestStore,
        IMcpRuntimeResolver runtimeResolver,
        IMcpDataDirectoryLayout layout)
    {
        _metadataStore = metadataStore;
        _manifestStore = manifestStore;
        _runtimeResolver = runtimeResolver;
        _layout = layout;
    }

    public async Task<RepairResult> RepairInstallAsync(Guid installId, CancellationToken cancellationToken = default)
    {
        var installed = await _metadataStore.GetAsync(installId, cancellationToken)
            ?? throw new KeyNotFoundException($"Install '{installId}' does not exist.");

        var manifest = await _manifestStore.ReadAsync(installed.ManifestPath, cancellationToken);
        if (manifest == null)
        {
            installed.InstallState = InstallState.Failed;
            installed.LastError = $"Manifest '{installed.ManifestPath}' is missing.";
            installed.UpdatedAt = DateTime.UtcNow;
            await _metadataStore.UpsertAsync(installed, cancellationToken);

            return new RepairResult
            {
                InstalledMcp = installed,
                Repaired = false,
                Message = installed.LastError
            };
        }

        var installDirectory = Path.IsPathRooted(manifest.InstallDirectory)
            ? manifest.InstallDirectory
            : Path.GetFullPath(Path.Combine(_layout.GetDataRoot(), manifest.InstallDirectory));
        if (manifest.TransportType == McpTransportType.Stdio && !Directory.Exists(installDirectory))
        {
            installed.InstallState = InstallState.Failed;
            installed.LastError = $"Install directory '{installDirectory}' is missing.";
            installed.UpdatedAt = DateTime.UtcNow;
            await _metadataStore.UpsertAsync(installed, cancellationToken);

            return new RepairResult
            {
                InstalledMcp = installed,
                Repaired = false,
                Message = installed.LastError
            };
        }

        await _runtimeResolver.ResolveRuntimeAsync(installId, cancellationToken);

        installed.InstallState = InstallState.Ready;
        installed.LastError = null;
        installed.UpdatedAt = DateTime.UtcNow;
        await _metadataStore.UpsertAsync(installed, cancellationToken);

        return new RepairResult
        {
            InstalledMcp = installed,
            Repaired = true,
            Message = "Install metadata and runtime manifest are consistent."
        };
    }
}
