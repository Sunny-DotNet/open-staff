using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenStaff.Mcp.Cli;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Persistence;

namespace OpenStaff.Mcp.PackageManagers;

/// <summary>
/// zh-CN: 将 npm 包安装到受管目录，并基于包的 bin 字段生成 `stdio` 运行时规格。
/// en: Installs npm packages into the managed directory and builds a stdio runtime spec from the package's bin definition.
/// </summary>
public sealed class NpmInstallChannelInstaller : IInstallChannelInstaller
{
    private readonly ICommandRunner _commandRunner;
    private readonly OpenStaffMcpOptions _options;

    public NpmInstallChannelInstaller(ICommandRunner commandRunner, IOptions<OpenStaffMcpOptions> options)
    {
        _commandRunner = commandRunner;
        _options = options.Value;
    }

    public IReadOnlyCollection<McpChannelType> SupportedChannelTypes { get; } = [McpChannelType.Npm];

    public async Task<InstallerResult> InstallAsync(InstallExecutionContext context, CancellationToken cancellationToken = default)
    {
        await context.UpdateStateAsync(InstallState.Installing, null);

        var packageIdentifier = context.Channel.PackageIdentifier;
        if (string.IsNullOrWhiteSpace(packageIdentifier))
            throw new InvalidOperationException("npm installation requires PackageIdentifier.");

        Directory.CreateDirectory(context.InstallDirectory);

        var packageSpec = string.IsNullOrWhiteSpace(context.Request.RequestedVersion)
            ? packageIdentifier
            : $"{packageIdentifier}@{context.Request.RequestedVersion}";

        var result = await _commandRunner.RunAsync(
            _options.BootstrapNpmCommand,
            ["install", "--prefix", context.InstallDirectory, "--no-save", "--fund=false", "--audit=false", packageSpec],
            context.InstallDirectory,
            cancellationToken: cancellationToken);
        result.EnsureSuccess($"npm install {packageSpec}");

        var packageRoot = Path.Combine(context.InstallDirectory, "node_modules", packageIdentifier.Replace('/', Path.DirectorySeparatorChar));
        var packageJsonPath = Path.Combine(packageRoot, "package.json");
        if (!File.Exists(packageJsonPath))
            throw new InvalidOperationException($"Installed npm package '{packageIdentifier}' does not contain package.json.");

        await using var packageJsonStream = File.OpenRead(packageJsonPath);
        using var packageDocument = await JsonDocument.ParseAsync(packageJsonStream, cancellationToken: cancellationToken);
        var root = packageDocument.RootElement;

        var nodeExecutablePath = ResolveNodeExecutablePath();

        var binRelativePath = ResolveNpmEntrypoint(root, packageIdentifier, context.Channel.EntrypointHint);
        var packageVersion = root.TryGetProperty("version", out var versionElement)
            ? versionElement.GetString() ?? context.Channel.Version ?? "latest"
            : context.Channel.Version ?? "latest";

        var runtime = new PersistedRuntimeSpec
        {
            TransportType = McpTransportType.Stdio,
            Command = nodeExecutablePath,
            Arguments =
            [
                Path.Combine("node_modules", packageIdentifier.Replace('/', Path.DirectorySeparatorChar), NormalizeRelativePath(binRelativePath))
            ],
            ArgumentsRelativeToInstallDirectory = [0],
            WorkingDirectory = ".",
            WorkingDirectoryRelativeToInstallDirectory = true
        };

        return new InstallerResult
        {
            InstalledVersion = packageVersion,
            Runtime = runtime,
            Artifacts =
            [
                new ManagedArtifact
                {
                    ArtifactType = ManagedArtifactType.PackagePayload,
                    RelativePath = Path.Combine("node_modules", packageIdentifier.Replace('/', Path.DirectorySeparatorChar)),
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
    }

    private static string ResolveNpmEntrypoint(JsonElement packageManifest, string packageIdentifier, string? entrypointHint)
    {
        if (!packageManifest.TryGetProperty("bin", out var binElement))
            throw new InvalidOperationException($"npm package '{packageIdentifier}' does not expose a bin entry.");

        if (binElement.ValueKind == JsonValueKind.String)
            return binElement.GetString() ?? throw new InvalidOperationException($"npm package '{packageIdentifier}' exposes an empty bin entry.");

        if (binElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"npm package '{packageIdentifier}' has an unsupported bin definition.");

        var bins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in binElement.EnumerateObject())
        {
            if (!string.IsNullOrWhiteSpace(property.Value.GetString()))
                bins[property.Name] = property.Value.GetString()!;
        }

        if (!string.IsNullOrWhiteSpace(entrypointHint))
        {
            if (bins.TryGetValue(entrypointHint, out var hintedBin))
                return hintedBin;

            var pathMatch = bins.Values.FirstOrDefault(value => string.Equals(value, entrypointHint, StringComparison.OrdinalIgnoreCase));
            if (pathMatch != null)
                return pathMatch;
        }

        if (bins.Count == 1)
            return bins.Values.Single();

        var packageLeaf = packageIdentifier.Split('/').Last();
        if (bins.TryGetValue(packageLeaf, out var leafBin))
            return leafBin;

        throw new InvalidOperationException(
            $"npm package '{packageIdentifier}' exposes multiple bin entries and no EntrypointHint was supplied.");
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar);
    }

    private string ResolveNodeExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_options.ManagedNodeExecutablePath))
            return Path.GetFullPath(ExecutablePathResolver.ResolveExecutablePath(_options.ManagedNodeExecutablePath));

        var resolved = ExecutablePathResolver.ResolveExecutablePath("node");
        if (OperatingSystem.IsWindows() && !Path.IsPathRooted(resolved))
        {
            throw new InvalidOperationException(
                "ManagedNodeExecutablePath must be configured or 'node' must be available on PATH for npm-based MCP runtime resolution.");
        }

        return Path.IsPathRooted(resolved) ? Path.GetFullPath(resolved) : resolved;
    }
}
