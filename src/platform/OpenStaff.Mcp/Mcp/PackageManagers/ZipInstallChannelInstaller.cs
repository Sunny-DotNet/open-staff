using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenStaff.Mcp.Cli;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Persistence;

namespace OpenStaff.Mcp.PackageManagers;

/// <summary>
/// zh-CN: 处理 GitHub Release / zip 类型的受管下载、解压与运行时推导。
/// en: Handles managed download, extraction, and runtime inference for GitHub Release / zip channels.
/// </summary>
public sealed class ZipInstallChannelInstaller : IInstallChannelInstaller
{
    private readonly IArtifactDownloader _artifactDownloader;
    private readonly IZipExtractor _zipExtractor;
    private readonly IMcpDataDirectoryLayout _layout;
    private readonly OpenStaffMcpOptions _options;

    public ZipInstallChannelInstaller(
        IArtifactDownloader artifactDownloader,
        IZipExtractor zipExtractor,
        IMcpDataDirectoryLayout layout,
        IOptions<OpenStaffMcpOptions> options)
    {
        _artifactDownloader = artifactDownloader;
        _zipExtractor = zipExtractor;
        _layout = layout;
        _options = options.Value;
    }

    public IReadOnlyCollection<McpChannelType> SupportedChannelTypes { get; } = [McpChannelType.GithubRelease, McpChannelType.Zip];

    public async Task<InstallerResult> InstallAsync(InstallExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Channel.ArtifactUrl))
            throw new InvalidOperationException("Zip-based installation requires ArtifactUrl.");

        await context.UpdateStateAsync(InstallState.Downloading, null);

        var downloadPath = _layout.GetDownloadCachePath(context.InstallId, ".zip");
        var extractPath = _layout.GetExtractCachePath(context.InstallId);
        await _artifactDownloader.DownloadAsync(
            new Uri(context.Channel.ArtifactUrl, UriKind.Absolute),
            downloadPath,
            context.Channel.Checksum,
            cancellationToken);

        await context.UpdateStateAsync(InstallState.Extracting, null);

        await _zipExtractor.ExtractAsync(downloadPath, extractPath, cancellationToken);
        var extractedRoot = ResolveContentRoot(extractPath);
        DirectoryCopyHelper.Copy(extractedRoot, context.InstallDirectory);

        await context.UpdateStateAsync(InstallState.ResolvingRuntime, null);

        var runtime = BuildRuntime(context.Channel);
        var version = context.Request.RequestedVersion ?? context.Channel.Version ?? context.CatalogEntry.Version ?? "latest";

        return new InstallerResult
        {
            InstalledVersion = version,
            Runtime = runtime,
            Artifacts =
            [
                new ManagedArtifact
                {
                    ArtifactType = ManagedArtifactType.DownloadCache,
                    RelativePath = _layout.GetRelativePathFromDataRoot(downloadPath),
                    Checksum = context.Channel.Checksum,
                    CreatedAt = DateTime.UtcNow
                },
                new ManagedArtifact
                {
                    ArtifactType = ManagedArtifactType.ExtractCache,
                    RelativePath = _layout.GetRelativePathFromDataRoot(extractPath),
                    CreatedAt = DateTime.UtcNow
                },
                new ManagedArtifact
                {
                    ArtifactType = ManagedArtifactType.InstallDirectory,
                    RelativePath = Path.GetFileName(context.InstallDirectory),
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
    }

    private PersistedRuntimeSpec BuildRuntime(InstallChannel channel)
    {
        if (channel.Metadata.TryGetValue(InstallChannelMetadataKeys.RuntimeCommand, out var runtimeCommand)
            && !string.IsNullOrWhiteSpace(runtimeCommand))
        {
            var arguments = TryDeserialize<List<string>>(channel.Metadata, InstallChannelMetadataKeys.RuntimeArguments) ?? [];
            var environmentVariables = TryDeserialize<Dictionary<string, string?>>(channel.Metadata, InstallChannelMetadataKeys.RuntimeEnvironment)
                ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var relativeArguments = TryDeserialize<List<int>>(channel.Metadata, InstallChannelMetadataKeys.RuntimeArgumentsRelativeIndexes) ?? [];
            var commandRelative = TryParseBoolean(channel.Metadata, InstallChannelMetadataKeys.RuntimeCommandRelative);

            return new PersistedRuntimeSpec
            {
                TransportType = McpTransportType.Stdio,
                Command = runtimeCommand,
                CommandRelativeToInstallDirectory = commandRelative,
                Arguments = arguments,
                ArgumentsRelativeToInstallDirectory = relativeArguments,
                EnvironmentVariables = environmentVariables,
                WorkingDirectory = channel.Metadata.TryGetValue(InstallChannelMetadataKeys.RuntimeWorkingDirectory, out var workingDirectory)
                    ? workingDirectory
                    : ".",
                WorkingDirectoryRelativeToInstallDirectory = true
            };
        }

        if (string.IsNullOrWhiteSpace(channel.EntrypointHint))
            throw new InvalidOperationException("Zip-based installation requires either runtime metadata or EntrypointHint.");

        var normalizedHint = channel.EntrypointHint.Replace('/', Path.DirectorySeparatorChar);
        if (normalizedHint.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return new PersistedRuntimeSpec
            {
                TransportType = McpTransportType.Stdio,
                Command = normalizedHint,
                CommandRelativeToInstallDirectory = true,
                WorkingDirectory = ".",
                WorkingDirectoryRelativeToInstallDirectory = true
            };
        }

        if (normalizedHint.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            return new PersistedRuntimeSpec
            {
                TransportType = McpTransportType.Stdio,
                Command = ResolveNodeExecutablePath(),
                Arguments = [normalizedHint],
                ArgumentsRelativeToInstallDirectory = [0],
                WorkingDirectory = ".",
                WorkingDirectoryRelativeToInstallDirectory = true
            };
        }

        throw new InvalidOperationException(
            $"EntrypointHint '{channel.EntrypointHint}' is not supported for zip-based installation. Supply explicit runtime metadata.");
    }

    private static string ResolveContentRoot(string extractPath)
    {
        var directories = Directory.GetDirectories(extractPath);
        var files = Directory.GetFiles(extractPath);
        return directories.Length == 1 && files.Length == 0 ? directories[0] : extractPath;
    }

    private static bool TryParseBoolean(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value)
            && bool.TryParse(value, out var parsed)
            && parsed;
    }

    private static T? TryDeserialize<T>(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value)
            ? JsonSerializer.Deserialize<T>(value, McpJsonSerializer.Options)
            : default;
    }

    private string ResolveNodeExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_options.ManagedNodeExecutablePath))
            return Path.GetFullPath(ExecutablePathResolver.ResolveExecutablePath(_options.ManagedNodeExecutablePath));

        var resolved = ExecutablePathResolver.ResolveExecutablePath("node");
        if (OperatingSystem.IsWindows() && !Path.IsPathRooted(resolved))
        {
            throw new InvalidOperationException(
                "ManagedNodeExecutablePath must be configured or 'node' must be available on PATH for JavaScript zip runtimes.");
        }

        return Path.IsPathRooted(resolved) ? Path.GetFullPath(resolved) : resolved;
    }
}

internal static class DirectoryCopyHelper
{
    public static void Copy(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }
}
