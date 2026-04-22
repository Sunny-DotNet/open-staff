using Microsoft.Extensions.Options;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Persistence;
using OpenStaff.Mcp.Services;

namespace OpenStaff.Tests.Unit;

public class McpRuntimeResolverTests
{
    /// <summary>
    /// zh-CN: 验证运行时解析会把 manifest 中相对于安装目录的路径还原成绝对路径。
    /// en: Verifies runtime resolution expands install-directory-relative paths from the manifest into absolute paths.
    /// </summary>
    [Fact]
    public async Task ResolveRuntimeAsync_ShouldExpandRelativePaths()
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
            var installId = Guid.NewGuid();
            var installDirectory = Path.Combine(layout.GetInstallsDirectory(), "npm", "pkg", "1.0.0");
            Directory.CreateDirectory(Path.Combine(installDirectory, "dist"));
            File.WriteAllText(Path.Combine(installDirectory, "dist", "index.js"), "console.log('ok');");

            var installed = new InstalledMcp
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
                InstallDirectory = installDirectory,
                ManifestPath = layout.GetManifestPath(installId),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await metadataStore.UpsertAsync(installed);
            await manifestStore.WriteAsync(installed.ManifestPath, new McpManifest
            {
                InstallId = installId,
                CatalogEntryId = "filesystem",
                Name = "filesystem",
                DisplayName = "Filesystem",
                SourceKey = "internal",
                ChannelType = McpChannelType.Npm,
                TransportType = McpTransportType.Stdio,
                Version = "1.0.0",
                InstallDirectory = layout.GetRelativePathFromDataRoot(installDirectory),
                Runtime = new PersistedRuntimeSpec
                {
                    TransportType = McpTransportType.Stdio,
                    Command = "C:\\managed\\node.exe",
                    Arguments = [Path.Combine("dist", "index.js")],
                    ArgumentsRelativeToInstallDirectory = [0],
                    WorkingDirectory = ".",
                    WorkingDirectoryRelativeToInstallDirectory = true
                }
            });

            var resolver = new McpRuntimeResolver(metadataStore, manifestStore, layout);

            var runtime = await resolver.ResolveRuntimeAsync(installId);

            Assert.Equal("C:\\managed\\node.exe", runtime.Command);
            Assert.Equal(Path.Combine(installDirectory, "dist", "index.js"), Assert.Single(runtime.Arguments));
            Assert.Equal(installDirectory, runtime.WorkingDirectory);
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
}
