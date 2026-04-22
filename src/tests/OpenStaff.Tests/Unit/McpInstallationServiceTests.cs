using Microsoft.Extensions.Options;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.PackageManagers;
using OpenStaff.Mcp.Persistence;
using OpenStaff.Mcp.Services;
using OpenStaff.Mcp.Sources;

namespace OpenStaff.Tests.Unit;

public class McpInstallationServiceTests
{
    /// <summary>
    /// zh-CN: 验证 remote 安装路径会落安装记录与 manifest，并把 endpoint 信息持久化到运行时规格。
    /// en: Verifies the remote-install flow persists both the install record and the manifest while carrying the endpoint metadata into the runtime spec.
    /// </summary>
    [Fact]
    public async Task InstallAsync_RemoteChannel_ShouldPersistManifest()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var layout = new McpDataDirectoryLayout(Microsoft.Extensions.Options.Options.Create(new OpenStaff.Mcp.OpenStaffMcpOptions
            {
                DataRootPath = tempRoot
            }));
            var metadataStore = new FileInstalledMcpMetadataStore(layout);
            var manifestStore = new FileMcpManifestStore();
            var service = new McpInstallationService(
                [new RemoteCatalogSource()],
                [],
                metadataStore,
                manifestStore,
                layout,
                new FileInstallLockManager(layout));

            var installed = await service.InstallAsync(new InstallRequest
            {
                SourceKey = "registry",
                CatalogEntryId = "remote-server",
                SelectedChannelId = "remote"
            });

            Assert.Equal(InstallState.Ready, installed.InstallState);

            var manifest = await manifestStore.ReadAsync(installed.ManifestPath);
            Assert.NotNull(manifest);
            Assert.Equal("https://example.test/mcp", manifest!.Runtime.Url);
            Assert.Equal(McpTransportType.StreamableHttp, manifest.Runtime.TransportType);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "openstaff-mcp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RemoteCatalogSource : IMcpCatalogSource
    {
        public string SourceKey => "registry";

        public string DisplayName => "Registry";

        public int Priority => 0;

        public Task<IReadOnlyList<CatalogEntry>> SearchAsync(CatalogSearchQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CatalogEntry>>([]);

        public Task<CatalogEntry?> GetByIdAsync(string entryId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CatalogEntry?>(new CatalogEntry
            {
                EntryId = "remote-server",
                SourceKey = "registry",
                Name = "remote-server",
                DisplayName = "Remote Server",
                TransportTypes = [McpTransportType.StreamableHttp],
                InstallChannels =
                [
                    new InstallChannel
                    {
                        ChannelId = "remote",
                        ChannelType = McpChannelType.Remote,
                        TransportType = McpTransportType.StreamableHttp,
                        ArtifactUrl = "https://example.test/mcp"
                    }
                ]
            });
        }
    }
}
