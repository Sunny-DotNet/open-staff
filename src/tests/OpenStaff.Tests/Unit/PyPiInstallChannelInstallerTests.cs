using Microsoft.Extensions.Options;
using OpenStaff.Mcp;
using OpenStaff.Mcp.Cli;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.PackageManagers;

namespace OpenStaff.Tests.Unit;

public class PyPiInstallChannelInstallerTests
{
    [Fact]
    public async Task InstallAsync_ShouldUseManagedPythonWrapperInsteadOfConsoleLauncher()
    {
        var tempRoot = CreateTempRoot();
        var installDirectory = Path.Combine(tempRoot, "install");
        Directory.CreateDirectory(installDirectory);

        try
        {
            var installer = new PyPiInstallChannelInstaller(
                new StubCommandRunner(),
                Microsoft.Extensions.Options.Options.Create(new OpenStaffMcpOptions
                {
                    BootstrapPythonCommand = "python"
                }));

            var result = await installer.InstallAsync(new InstallExecutionContext
            {
                InstallId = Guid.NewGuid(),
                Request = new InstallRequest
                {
                    CatalogEntryId = "builtin.fetch.legacy",
                    SourceKey = "mcps"
                },
                CatalogEntry = new CatalogEntry
                {
                    EntryId = "builtin.fetch.legacy",
                    SourceKey = "mcps",
                    Name = "fetch",
                    DisplayName = "Fetch"
                },
                Channel = new InstallChannel
                {
                    ChannelId = "package-python",
                    ChannelType = McpChannelType.Pypi,
                    TransportType = McpTransportType.Stdio,
                    PackageIdentifier = "mcp-server-fetch"
                },
                InstallDirectory = installDirectory,
                UpdateStateAsync = (_, _) => Task.CompletedTask
            });

            Assert.Equal("2025.4.7", result.InstalledVersion);
            Assert.Equal(McpTransportType.Stdio, result.Runtime.TransportType);
            Assert.Equal(Path.Combine(".venv", "Scripts", "python.exe"), result.Runtime.Command);
            Assert.Equal(["openstaff-mcp-entrypoint.py"], result.Runtime.Arguments);
            Assert.True(result.Runtime.CommandRelativeToInstallDirectory);
            Assert.Equal([0], result.Runtime.ArgumentsRelativeToInstallDirectory);
            Assert.Equal(".", result.Runtime.WorkingDirectory);
            Assert.True(result.Runtime.WorkingDirectoryRelativeToInstallDirectory);

            var launcherPath = Path.Combine(installDirectory, "openstaff-mcp-entrypoint.py");
            Assert.True(File.Exists(launcherPath));

            var launcherScript = await File.ReadAllTextAsync(launcherPath);
            Assert.Contains("metadata.EntryPoint", launcherScript);
            Assert.Contains("mcp-server-fetch", launcherScript);
            Assert.Contains("mcp_server_fetch:main", launcherScript);

            var runtimeBinary = Assert.Single(result.Artifacts, item => item.ArtifactType == ManagedArtifactType.RuntimeBinary);
            Assert.Equal("openstaff-mcp-entrypoint.py", runtimeBinary.RelativePath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "openstaff-pypi-installer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubCommandRunner : ICommandRunner
    {
        public Task<CommandExecutionResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory = null,
            IReadOnlyDictionary<string, string?>? environmentVariables = null,
            CancellationToken cancellationToken = default)
        {
            if (arguments.Count >= 3
                && arguments[0] == "-m"
                && arguments[1] == "venv")
            {
                var venvDirectory = arguments[2];
                Directory.CreateDirectory(Path.Combine(venvDirectory, "Scripts"));
                File.WriteAllText(Path.Combine(venvDirectory, "Scripts", "python.exe"), string.Empty);
                return Task.FromResult(new CommandExecutionResult { ExitCode = 0 });
            }

            if (arguments.Count >= 3
                && arguments[0] == "-m"
                && arguments[1] == "pip"
                && arguments[2] == "install")
            {
                return Task.FromResult(new CommandExecutionResult { ExitCode = 0 });
            }

            if (arguments.Count >= 3
                && arguments[0] == "-c")
            {
                return Task.FromResult(new CommandExecutionResult
                {
                    ExitCode = 0,
                    StandardOutput = """
                        {"version":"2025.4.7","entryPoints":[{"name":"mcp-server-fetch","value":"mcp_server_fetch:main"}]}
                        """
                });
            }

            throw new InvalidOperationException($"Unexpected command: {fileName} {string.Join(' ', arguments)}");
        }
    }
}
