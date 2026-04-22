using OpenStaff.Mcp.Exceptions;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Persistence;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: 从持久化 manifest 解析最终运行时规格，拒绝通过 PATH 或重新扫描目录进行猜测。
/// en: Resolves the final runtime spec from the persisted manifest and refuses to guess via PATH lookup or directory rescans.
/// </summary>
public sealed class McpRuntimeResolver : IMcpRuntimeResolver
{
    private readonly IInstalledMcpMetadataStore _metadataStore;
    private readonly IMcpManifestStore _manifestStore;
    private readonly IMcpDataDirectoryLayout _layout;

    public McpRuntimeResolver(
        IInstalledMcpMetadataStore metadataStore,
        IMcpManifestStore manifestStore,
        IMcpDataDirectoryLayout layout)
    {
        _metadataStore = metadataStore;
        _manifestStore = manifestStore;
        _layout = layout;
    }

    public async Task<RuntimeSpec> ResolveRuntimeAsync(Guid installId, CancellationToken cancellationToken = default)
    {
        var installed = await _metadataStore.GetAsync(installId, cancellationToken)
            ?? throw new RuntimeResolutionException($"Install '{installId}' does not exist.");
        if (installed.InstallState != InstallState.Ready)
            throw new RuntimeResolutionException(
                $"Install '{installId}' is not runnable because its state is '{installed.InstallState}'.");

        var manifest = await _manifestStore.ReadAsync(installed.ManifestPath, cancellationToken)
            ?? throw new RuntimeResolutionException($"Manifest '{installed.ManifestPath}' is missing.");

        var installDirectory = Path.IsPathRooted(manifest.InstallDirectory)
            ? manifest.InstallDirectory
            : Path.GetFullPath(Path.Combine(_layout.GetDataRoot(), manifest.InstallDirectory));

        if (manifest.TransportType == McpTransportType.Stdio && !Directory.Exists(installDirectory))
            throw new RuntimeResolutionException($"Install directory '{installDirectory}' is missing.");

        return ResolvePersistedRuntime(manifest.Runtime, installDirectory);
    }

    private static RuntimeSpec ResolvePersistedRuntime(PersistedRuntimeSpec persistedRuntime, string installDirectory)
    {
        if (persistedRuntime.TransportType != McpTransportType.Stdio)
        {
            if (string.IsNullOrWhiteSpace(persistedRuntime.Url))
                throw new RuntimeResolutionException("Remote runtime manifest is missing the endpoint URL.");

            return new RuntimeSpec
            {
                TransportType = persistedRuntime.TransportType,
                Url = persistedRuntime.Url,
                Headers = persistedRuntime.Headers
            };
        }

        if (string.IsNullOrWhiteSpace(persistedRuntime.Command))
            throw new RuntimeResolutionException("Stdio runtime manifest is missing the command.");

        var command = persistedRuntime.CommandRelativeToInstallDirectory
            ? Path.GetFullPath(Path.Combine(installDirectory, persistedRuntime.Command))
            : persistedRuntime.Command;
        var arguments = persistedRuntime.Arguments
            .Select((argument, index) => persistedRuntime.ArgumentsRelativeToInstallDirectory.Contains(index)
                ? Path.GetFullPath(Path.Combine(installDirectory, argument))
                : argument)
            .ToList();
        var workingDirectory = ResolveWorkingDirectory(persistedRuntime, installDirectory);

        return new RuntimeSpec
        {
            TransportType = persistedRuntime.TransportType,
            Command = command,
            Arguments = arguments,
            EnvironmentVariables = persistedRuntime.EnvironmentVariables,
            WorkingDirectory = workingDirectory
        };
    }

    private static string ResolveWorkingDirectory(PersistedRuntimeSpec persistedRuntime, string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(persistedRuntime.WorkingDirectory))
            return installDirectory;

        return persistedRuntime.WorkingDirectoryRelativeToInstallDirectory
            ? Path.GetFullPath(Path.Combine(installDirectory, persistedRuntime.WorkingDirectory))
            : persistedRuntime.WorkingDirectory;
    }
}
