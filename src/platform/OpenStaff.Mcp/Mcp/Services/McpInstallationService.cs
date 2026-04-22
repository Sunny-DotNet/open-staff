using System.Text.Json;
using OpenStaff.Mcp.Exceptions;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.PackageManagers;
using OpenStaff.Mcp.Persistence;
using OpenStaff.Mcp.Sources;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: 统一编排 remote 与 `stdio` 安装路径，负责状态持久化、manifest 写入和安装目录准备。
/// en: Orchestrates both remote and stdio installation paths, including state persistence, manifest writing, and install-directory preparation.
/// </summary>
public sealed class McpInstallationService : IMcpInstallationService
{
    private readonly IReadOnlyList<IMcpCatalogSource> _sources;
    private readonly IReadOnlyList<IInstallChannelInstaller> _installers;
    private readonly IInstalledMcpMetadataStore _metadataStore;
    private readonly IMcpManifestStore _manifestStore;
    private readonly IMcpDataDirectoryLayout _layout;
    private readonly IInstallLockManager _lockManager;

    public McpInstallationService(
        IEnumerable<IMcpCatalogSource> sources,
        IEnumerable<IInstallChannelInstaller> installers,
        IInstalledMcpMetadataStore metadataStore,
        IMcpManifestStore manifestStore,
        IMcpDataDirectoryLayout layout,
        IInstallLockManager lockManager)
    {
        _sources = sources.ToList();
        _installers = installers.ToList();
        _metadataStore = metadataStore;
        _manifestStore = manifestStore;
        _layout = layout;
        _lockManager = lockManager;
    }

    public async Task<InstalledMcp> InstallAsync(InstallRequest request, CancellationToken cancellationToken = default)
    {
        var source = ResolveSource(request.SourceKey);
        var entry = await source.GetByIdAsync(request.CatalogEntryId, cancellationToken)
            ?? throw new CatalogEntryNotFoundException(request.SourceKey, request.CatalogEntryId);
        var channel = SelectChannel(entry, request);
        var packageIdentifier = channel.PackageIdentifier ?? entry.Name;
        var requestedVersion = request.RequestedVersion ?? channel.Version ?? entry.Version ?? "latest";

        await using var installLock = await _lockManager.AcquireAsync($"{request.SourceKey}:{request.CatalogEntryId}", cancellationToken);

        var existing = await _metadataStore.GetByCatalogEntryAsync(request.SourceKey, request.CatalogEntryId, cancellationToken);
        if (existing != null && !request.OverwriteExisting)
            throw new InvalidOperationException($"Catalog entry '{request.CatalogEntryId}' from source '{request.SourceKey}' is already installed.");

        var installId = existing?.InstallId ?? Guid.NewGuid();
        var installDirectory = _layout.GetInstallDirectory(channel, packageIdentifier, requestedVersion, request.InstallRoot);
        var manifestPath = _layout.GetManifestPath(installId);
        var installed = existing ?? CreateInstallRecord(installId, entry, channel, requestedVersion, installDirectory, manifestPath);

        installed.CatalogEntryId = entry.EntryId;
        installed.Name = entry.Name;
        installed.DisplayName = entry.DisplayName;
        installed.SourceKey = entry.SourceKey;
        installed.ChannelType = channel.ChannelType;
        installed.TransportType = channel.TransportType;
        installed.Version = requestedVersion;
        installed.InstallDirectory = installDirectory;
        installed.ManifestPath = manifestPath;
        installed.CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow;
        installed.UpdatedAt = DateTime.UtcNow;
        installed.LastError = null;

        async Task UpdateStateAsync(InstallState state, string? error)
        {
            installed.InstallState = state;
            installed.LastError = error;
            installed.UpdatedAt = DateTime.UtcNow;
            await _metadataStore.UpsertAsync(installed, cancellationToken);
        }

        await UpdateStateAsync(InstallState.Pending, null);

        try
        {
            PrepareInstallDirectory(existing?.InstallDirectory, installDirectory);

            InstallerResult installerResult;
            if (channel.ChannelType == McpChannelType.Remote)
            {
                await UpdateStateAsync(InstallState.ResolvingRuntime, null);
                installerResult = BuildRemoteInstallerResult(entry, channel, request.RequestedVersion);
            }
            else
            {
                var installer = ResolveInstaller(channel.ChannelType);
                installerResult = await installer.InstallAsync(
                    new InstallExecutionContext
                    {
                        InstallId = installId,
                        Request = request,
                        CatalogEntry = entry,
                        Channel = channel,
                        InstallDirectory = installDirectory,
                        UpdateStateAsync = UpdateStateAsync
                    },
                    cancellationToken);
            }

            if (!string.Equals(requestedVersion, installerResult.InstalledVersion, StringComparison.OrdinalIgnoreCase)
                && string.Equals(requestedVersion, "latest", StringComparison.OrdinalIgnoreCase))
            {
                installDirectory = MoveInstallDirectoryIfVersionMaterialized(channel, packageIdentifier, request, installDirectory, installerResult.InstalledVersion);
                installed.InstallDirectory = installDirectory;
            }

            installed.Version = installerResult.InstalledVersion;
            var artifacts = installerResult.Artifacts.ToList();
            PersistSourceMetadata(channel, installDirectory, artifacts);

            var manifest = new McpManifest
            {
                InstallId = installId,
                CatalogEntryId = entry.EntryId,
                Name = entry.Name,
                DisplayName = entry.DisplayName,
                SourceKey = entry.SourceKey,
                ChannelType = channel.ChannelType,
                TransportType = channel.TransportType,
                Version = installerResult.InstalledVersion,
                InstallDirectory = _layout.GetRelativePathFromDataRoot(installDirectory),
                Runtime = installerResult.Runtime,
                Artifacts = artifacts,
                CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _manifestStore.WriteAsync(manifestPath, manifest, cancellationToken);
            await UpdateStateAsync(InstallState.Ready, null);

            return installed;
        }
        catch (Exception ex)
        {
            await UpdateStateAsync(InstallState.Failed, ex.Message);
            throw;
        }
    }

    private IMcpCatalogSource ResolveSource(string sourceKey)
    {
        return _sources.FirstOrDefault(source => string.Equals(source.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase))
               ?? throw new CatalogEntryNotFoundException(sourceKey, string.Empty);
    }

    private static InstallChannel SelectChannel(CatalogEntry entry, InstallRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SelectedChannelId))
        {
            return entry.InstallChannels.FirstOrDefault(channel =>
                       string.Equals(channel.ChannelId, request.SelectedChannelId, StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException(
                       $"Channel '{request.SelectedChannelId}' was not found for catalog entry '{entry.EntryId}'.");
        }

        if (entry.InstallChannels.Count == 1)
            return entry.InstallChannels[0];

        if (!string.IsNullOrWhiteSpace(request.RequestedVersion))
        {
            var exactMatches = entry.InstallChannels
                .Where(channel => string.Equals(channel.Version, request.RequestedVersion, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (exactMatches.Count == 1)
                return exactMatches[0];
        }

        throw new InvalidOperationException(
            $"Catalog entry '{entry.EntryId}' exposes multiple install channels; SelectedChannelId is required.");
    }

    private IInstallChannelInstaller ResolveInstaller(McpChannelType channelType)
    {
        return _installers.FirstOrDefault(installer => installer.SupportedChannelTypes.Contains(channelType))
               ?? throw new InstallChannelNotSupportedException(channelType);
    }

    private static InstalledMcp CreateInstallRecord(
        Guid installId,
        CatalogEntry entry,
        InstallChannel channel,
        string version,
        string installDirectory,
        string manifestPath)
    {
        return new InstalledMcp
        {
            InstallId = installId,
            CatalogEntryId = entry.EntryId,
            Name = entry.Name,
            DisplayName = entry.DisplayName,
            SourceKey = entry.SourceKey,
            ChannelType = channel.ChannelType,
            TransportType = channel.TransportType,
            Version = version,
            InstallState = InstallState.Pending,
            InstallDirectory = installDirectory,
            ManifestPath = manifestPath,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static InstallerResult BuildRemoteInstallerResult(CatalogEntry entry, InstallChannel channel, string? requestedVersion)
    {
        var endpointUrl = channel.Metadata.TryGetValue(InstallChannelMetadataKeys.EndpointUrl, out var metadataUrl)
            ? metadataUrl
            : channel.ArtifactUrl;
        if (string.IsNullOrWhiteSpace(endpointUrl))
            throw new InvalidOperationException($"Remote channel '{channel.ChannelId}' does not define an endpoint URL.");

        var headers = channel.Metadata.TryGetValue(InstallChannelMetadataKeys.EndpointHeaders, out var headersJson)
            ? JsonSerializer.Deserialize<Dictionary<string, string?>>(headersJson, McpJsonSerializer.Options)
              ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        return new InstallerResult
        {
            InstalledVersion = requestedVersion ?? channel.Version ?? entry.Version ?? "remote",
            Runtime = new PersistedRuntimeSpec
            {
                TransportType = channel.TransportType,
                Url = endpointUrl,
                Headers = headers
            }
        };
    }

    private static void PrepareInstallDirectory(string? previousInstallDirectory, string installDirectory)
    {
        if (!string.IsNullOrWhiteSpace(previousInstallDirectory)
            && !string.Equals(previousInstallDirectory, installDirectory, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(previousInstallDirectory))
        {
            Directory.Delete(previousInstallDirectory, recursive: true);
        }

        if (Directory.Exists(installDirectory))
            Directory.Delete(installDirectory, recursive: true);

        Directory.CreateDirectory(installDirectory);
    }

    private static void PersistSourceMetadata(
        InstallChannel channel,
        string installDirectory,
        ICollection<ManagedArtifact> artifacts)
    {
        if (!channel.Metadata.TryGetValue(McpSourceMetadataKeys.RawTemplateJson, out var rawTemplateJson)
            || string.IsNullOrWhiteSpace(rawTemplateJson))
        {
            return;
        }

        var sourceMetadataPath = Path.Combine(installDirectory, "source-template.json");
        File.WriteAllText(sourceMetadataPath, rawTemplateJson);

        artifacts.Add(new ManagedArtifact
        {
            ArtifactType = ManagedArtifactType.SourceMetadata,
            RelativePath = Path.GetFileName(sourceMetadataPath),
            CreatedAt = DateTime.UtcNow
        });
    }

    private string MoveInstallDirectoryIfVersionMaterialized(
        InstallChannel channel,
        string packageIdentifier,
        InstallRequest request,
        string currentInstallDirectory,
        string installedVersion)
    {
        var finalInstallDirectory = _layout.GetInstallDirectory(channel, packageIdentifier, installedVersion, request.InstallRoot);
        if (string.Equals(finalInstallDirectory, currentInstallDirectory, StringComparison.OrdinalIgnoreCase))
            return currentInstallDirectory;

        if (Directory.Exists(finalInstallDirectory))
            Directory.Delete(finalInstallDirectory, recursive: true);

        Directory.CreateDirectory(Path.GetDirectoryName(finalInstallDirectory)!);
        Directory.Move(currentInstallDirectory, finalInstallDirectory);
        return finalInstallDirectory;
    }
}
