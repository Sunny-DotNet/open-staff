using OpenStaff.Mcp.Exceptions;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Persistence;
using OpenStaff.Mcp.Services;

namespace OpenStaff.Tests.Unit;

public class McpUninstallServiceTests
{
    /// <summary>
    /// zh-CN: 验证只要检查器报告仍有引用，卸载就会被阻止。
    /// en: Verifies uninstall is blocked as soon as a reference inspector reports active references.
    /// </summary>
    [Fact]
    public async Task UninstallAsync_WhenReferenced_ShouldThrow()
    {
        var installId = Guid.NewGuid();
        var metadataStore = new InMemoryMetadataStore(
        [
            new InstalledMcp
            {
                InstallId = installId,
                CatalogEntryId = "filesystem",
                Name = "filesystem",
                DisplayName = "Filesystem",
                SourceKey = "internal",
                ChannelType = McpChannelType.Npm,
                TransportType = McpTransportType.Stdio,
                Version = "1.0.0",
                InstallState = InstallState.Ready,
                ManifestPath = "manifest.json",
                InstallDirectory = "installs",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        ]);
        var service = new McpUninstallService(
            metadataStore,
            new NoopManifestStore(),
            new StubLayout(),
            [new BlockingReferenceInspector()]);

        await Assert.ThrowsAsync<UninstallBlockedException>(() => service.UninstallAsync(installId));
    }

    private sealed class BlockingReferenceInspector : IMcpReferenceInspector
    {
        public Task<McpReferenceInspectionResult> InspectAsync(InstalledMcp installedMcp, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new McpReferenceInspectionResult
            {
                BlockingReasons = ["Used by project binding"],
                ReferencedByProjectBindings = ["project-agent:demo"]
            });
        }
    }

    private sealed class NoopManifestStore : IMcpManifestStore
    {
        public Task<McpManifest?> ReadAsync(string path, CancellationToken cancellationToken = default)
            => Task.FromResult<McpManifest?>(null);

        public Task WriteAsync(string path, McpManifest manifest, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubLayout : IMcpDataDirectoryLayout
    {
        public string GetDataRoot() => Path.GetTempPath();
        public string GetInstallsDirectory() => Path.GetTempPath();
        public string GetMetadataDirectory() => Path.GetTempPath();
        public string GetManifestDirectory() => Path.GetTempPath();
        public string GetDownloadsCacheDirectory() => Path.GetTempPath();
        public string GetExtractsCacheDirectory() => Path.GetTempPath();
        public string GetLocksDirectory() => Path.GetTempPath();
        public string GetMetadataPath(Guid installId) => string.Empty;
        public string GetManifestPath(Guid installId) => string.Empty;
        public string GetDownloadCachePath(Guid installId, string extension) => string.Empty;
        public string GetExtractCachePath(Guid installId) => string.Empty;
        public string GetInstallDirectory(InstallChannel channel, string packageIdentifier, string version, string? installRootOverride = null) => string.Empty;
        public string GetRelativePathFromDataRoot(string fullPath) => fullPath;
    }

    private sealed class InMemoryMetadataStore : IInstalledMcpMetadataStore
    {
        private readonly List<InstalledMcp> _items;

        public InMemoryMetadataStore(IEnumerable<InstalledMcp> items)
        {
            _items = items.ToList();
        }

        public Task<IReadOnlyList<InstalledMcp>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InstalledMcp>>(_items);

        public Task<InstalledMcp?> GetAsync(Guid installId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(item => item.InstallId == installId));

        public Task<InstalledMcp?> GetByCatalogEntryAsync(string sourceKey, string catalogEntryId, CancellationToken cancellationToken = default)
            => Task.FromResult<InstalledMcp?>(null);

        public Task UpsertAsync(InstalledMcp installedMcp, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(Guid installId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
