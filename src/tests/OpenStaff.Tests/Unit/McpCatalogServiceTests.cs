using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Persistence;
using OpenStaff.Mcp.Services;
using OpenStaff.Mcp.Sources;

namespace OpenStaff.Tests.Unit;

public class McpCatalogServiceTests
{
    /// <summary>
    /// zh-CN: 验证搜索聚合后会将本地安装状态叠加回目录条目，而不是要求宿主再手工拼接。
    /// en: Verifies catalog search overlays local installation state so hosts do not need to stitch the status back manually.
    /// </summary>
    [Fact]
    public async Task SearchCatalogAsync_ShouldOverlayInstalledState()
    {
        var service = new McpCatalogService(
            [new FakeCatalogSource()],
            new InMemoryMetadataStore(
            [
                new InstalledMcp
                {
                    InstallId = Guid.NewGuid(),
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
            ]));

        var result = await service.SearchCatalogAsync(new CatalogSearchQuery { SourceKey = "internal" });

        var item = Assert.Single(result.Items);
        Assert.True(item.IsInstalled);
        Assert.Equal(InstallState.Ready, item.InstalledState);
        Assert.Equal("1.0.0", item.InstalledVersion);
    }

    private sealed class FakeCatalogSource : IMcpCatalogSource
    {
        public string SourceKey => "internal";

        public string DisplayName => "Internal";

        public int Priority => 0;

        public Task<IReadOnlyList<CatalogEntry>> SearchAsync(CatalogSearchQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CatalogEntry>>(
            [
                new CatalogEntry
                {
                    EntryId = "filesystem",
                    SourceKey = "internal",
                    Name = "filesystem",
                    DisplayName = "Filesystem",
                    Description = "Managed filesystem MCP",
                    Category = "filesystem",
                    TransportTypes = [McpTransportType.Stdio],
                    InstallChannels =
                    [
                        new InstallChannel
                        {
                            ChannelId = "npm",
                            ChannelType = McpChannelType.Npm,
                            TransportType = McpTransportType.Stdio,
                            PackageIdentifier = "@scope/filesystem"
                        }
                    ]
                }
            ]);
        }

        public Task<CatalogEntry?> GetByIdAsync(string entryId, CancellationToken cancellationToken = default)
            => Task.FromResult<CatalogEntry?>(null);
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
        {
            return Task.FromResult(_items.FirstOrDefault(item =>
                string.Equals(item.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.CatalogEntryId, catalogEntryId, StringComparison.OrdinalIgnoreCase)));
        }

        public Task UpsertAsync(InstalledMcp installedMcp, CancellationToken cancellationToken = default)
        {
            var existing = _items.FindIndex(item => item.InstallId == installedMcp.InstallId);
            if (existing >= 0)
                _items[existing] = installedMcp;
            else
                _items.Add(installedMcp);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid installId, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(item => item.InstallId == installId);
            return Task.CompletedTask;
        }
    }
}
