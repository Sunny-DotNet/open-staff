using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenStaff.Mcp.Cli;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Persistence;

namespace OpenStaff.Mcp.PackageManagers;

/// <summary>
/// zh-CN: 为 PyPI 包创建受管虚拟环境，并从 console_scripts 入口生成运行时规格。
/// en: Creates a managed virtual environment for PyPI packages and builds the runtime spec from console_scripts entry points.
/// </summary>
public sealed class PyPiInstallChannelInstaller : IInstallChannelInstaller
{
    private const string ManagedEntrypointScriptName = "openstaff-mcp-entrypoint.py";
    private const string EntryPointProbeScript = """
        import importlib.metadata as metadata
        import json
        import sys

        dist = metadata.distribution(sys.argv[1])
        entry_points = [{"name": entry.name, "value": entry.value} for entry in dist.entry_points if entry.group == "console_scripts"]
        print(json.dumps({"version": dist.version, "entryPoints": entry_points}))
        """;

    private readonly ICommandRunner _commandRunner;
    private readonly OpenStaffMcpOptions _options;

    public PyPiInstallChannelInstaller(ICommandRunner commandRunner, IOptions<OpenStaffMcpOptions> options)
    {
        _commandRunner = commandRunner;
        _options = options.Value;
    }

    public IReadOnlyCollection<McpChannelType> SupportedChannelTypes { get; } = [McpChannelType.Pypi];

    public async Task<InstallerResult> InstallAsync(InstallExecutionContext context, CancellationToken cancellationToken = default)
    {
        await context.UpdateStateAsync(InstallState.Installing, null);

        var packageIdentifier = context.Channel.PackageIdentifier;
        if (string.IsNullOrWhiteSpace(packageIdentifier))
            throw new InvalidOperationException("PyPI installation requires PackageIdentifier.");

        Directory.CreateDirectory(context.InstallDirectory);
        var venvDirectory = Path.Combine(context.InstallDirectory, ".venv");

        var createVenvResult = await _commandRunner.RunAsync(
            _options.BootstrapPythonCommand,
            ["-m", "venv", venvDirectory],
            context.InstallDirectory,
            cancellationToken: cancellationToken);
        createVenvResult.EnsureSuccess($"python -m venv {venvDirectory}");

        var managedPythonPath = Path.Combine(venvDirectory, "Scripts", "python.exe");
        if (!File.Exists(managedPythonPath))
            throw new InvalidOperationException($"Managed Python executable was not created at '{managedPythonPath}'.");

        var packageSpec = string.IsNullOrWhiteSpace(context.Request.RequestedVersion)
            ? packageIdentifier
            : $"{packageIdentifier}=={context.Request.RequestedVersion}";
        var installResult = await _commandRunner.RunAsync(
            managedPythonPath,
            ["-m", "pip", "install", packageSpec],
            context.InstallDirectory,
            cancellationToken: cancellationToken);
        installResult.EnsureSuccess($"pip install {packageSpec}");

        var probeResult = await _commandRunner.RunAsync(
            managedPythonPath,
            ["-c", EntryPointProbeScript, packageIdentifier],
            context.InstallDirectory,
            cancellationToken: cancellationToken);
        probeResult.EnsureSuccess($"resolve console_scripts for {packageIdentifier}");

        var probePayload = JsonSerializer.Deserialize<PythonPackageProbeResult>(probeResult.StandardOutput, McpJsonSerializer.Options)
            ?? throw new InvalidOperationException($"Failed to parse PyPI probe result for '{packageIdentifier}'.");
        var entryPoint = ResolveEntryPoint(packageIdentifier, probePayload.EntryPoints, context.Channel.EntrypointHint);
        var launcherRelativePath = CreateManagedLauncher(context.InstallDirectory, entryPoint);

        return new InstallerResult
        {
            InstalledVersion = probePayload.Version,
            Runtime = new PersistedRuntimeSpec
            {
                TransportType = McpTransportType.Stdio,
                Command = Path.GetRelativePath(context.InstallDirectory, managedPythonPath),
                Arguments = [launcherRelativePath],
                CommandRelativeToInstallDirectory = true,
                ArgumentsRelativeToInstallDirectory = [0],
                WorkingDirectory = ".",
                WorkingDirectoryRelativeToInstallDirectory = true
            },
            Artifacts =
            [
                new ManagedArtifact
                {
                    ArtifactType = ManagedArtifactType.RuntimeBinary,
                    RelativePath = launcherRelativePath,
                    CreatedAt = DateTime.UtcNow
                },
                new ManagedArtifact
                {
                    ArtifactType = ManagedArtifactType.PackagePayload,
                    RelativePath = ".venv",
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
    }

    private static PythonPackageEntryPoint ResolveEntryPoint(
        string packageIdentifier,
        IReadOnlyList<PythonPackageEntryPoint> entryPoints,
        string? entrypointHint)
    {
        if (!string.IsNullOrWhiteSpace(entrypointHint)
            && entryPoints.Any(entry => string.Equals(entry.Name, entrypointHint, StringComparison.OrdinalIgnoreCase)))
        {
            return entryPoints.First(entry => string.Equals(entry.Name, entrypointHint, StringComparison.OrdinalIgnoreCase));
        }

        if (entryPoints.Count == 1)
            return entryPoints[0];

        var candidates = new[]
        {
            packageIdentifier,
            packageIdentifier.Replace('-', '_'),
            packageIdentifier.Split('/').Last()
        };

        foreach (var candidate in candidates)
        {
            var match = entryPoints.FirstOrDefault(entry => string.Equals(entry.Name, candidate, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }

        throw new InvalidOperationException(
            $"PyPI package '{packageIdentifier}' exposes multiple console scripts and no EntrypointHint was provided.");
    }

    private static string CreateManagedLauncher(string installDirectory, PythonPackageEntryPoint entryPoint)
    {
        if (string.IsNullOrWhiteSpace(entryPoint.Name) || string.IsNullOrWhiteSpace(entryPoint.Value))
            throw new InvalidOperationException("PyPI console_scripts entry point is missing name or value.");

        var launcherPath = Path.Combine(installDirectory, ManagedEntrypointScriptName);
        var entryPointNameJson = JsonSerializer.Serialize(entryPoint.Name);
        var entryPointValueJson = JsonSerializer.Serialize(entryPoint.Value);

        var script = $$"""
            import importlib.metadata as metadata
            import sys

            entry_point = metadata.EntryPoint(name={{entryPointNameJson}}, value={{entryPointValueJson}}, group="console_scripts")
            callable_obj = entry_point.load()
            result = callable_obj()
            raise SystemExit(result if isinstance(result, int) else 0)
            """;

        File.WriteAllText(launcherPath, script);
        return ManagedEntrypointScriptName;
    }

    private sealed class PythonPackageProbeResult
    {
        public string Version { get; init; } = string.Empty;

        public List<PythonPackageEntryPoint> EntryPoints { get; init; } = [];
    }

    private sealed class PythonPackageEntryPoint
    {
        public string Name { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;
    }
}
