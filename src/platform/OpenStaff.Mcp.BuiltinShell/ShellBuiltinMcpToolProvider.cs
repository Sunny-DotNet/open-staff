using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Win32;
using OpenStaff.Agent.Services;
using OpenStaff.Mcp.Builtin;
using OpenStaff.Mcp.Cli;

namespace OpenStaff.Mcp.BuiltinShell;

public sealed class ShellBuiltinMcpToolProvider : IBuiltinMcpToolProvider
{
    private const string ShellExecToolName = "shell.exec";
    private const string ShellExecDescription = "Execute a process inside the configured workspace and return exit code, stdout, stderr, and duration.";
    private const string ShellSystemInfoToolName = "shell.system_info";
    private const string ShellSystemInfoDescription = "Collect host system information including processor, memory, disks, network adapters, and installed software summary.";

    private readonly ICommandRunner _commandRunner;
    private readonly IPermissionRequestHandler _permissionRequestHandler;

    public ShellBuiltinMcpToolProvider(
        ICommandRunner commandRunner,
        IPermissionRequestHandler permissionRequestHandler)
    {
        _commandRunner = commandRunner;
        _permissionRequestHandler = permissionRequestHandler;
    }

    public string ProviderId => BuiltinShellMcpServerDefinition.ProviderId;

    public Task<IReadOnlyList<McpRuntimeToolDescriptor>> GetToolsAsync(
        ResolvedMcpClientConnection connection,
        CancellationToken cancellationToken = default)
    {
        var execTool = AIFunctionFactory.Create(
            (ShellExecRequest request, CancellationToken ct) => ExecuteAsync(connection, request, ct),
            ShellExecToolName,
            ShellExecDescription,
            serializerOptions: null);
        var systemInfoTool = AIFunctionFactory.Create(
            (ShellSystemInfoRequest request, CancellationToken ct) => GetSystemInfoAsync(connection, request, ct),
            ShellSystemInfoToolName,
            ShellSystemInfoDescription,
            serializerOptions: null);
        return Task.FromResult<IReadOnlyList<McpRuntimeToolDescriptor>>(
            [
                McpRuntimeToolDescriptor.FromAITool(execTool),
                McpRuntimeToolDescriptor.FromAITool(systemInfoTool)
            ]);
    }

    public Task<string?> GetPreloadSkipReasonAsync(
        ResolvedMcpClientConnection connection,
        CancellationToken cancellationToken = default)
    {
        var configuration = ParseConfiguration(connection);
        if (configuration.RestrictToWorkspace && string.IsNullOrWhiteSpace(configuration.WorkspacePath))
            return Task.FromResult<string?>("Builtin shell workspace is not configured.");

        return Task.FromResult<string?>(null);
    }

    private async Task<ShellExecResult> ExecuteAsync(
        ResolvedMcpClientConnection connection,
        ShellExecRequest request,
        CancellationToken cancellationToken)
    {
        var configuration = ParseConfiguration(connection);
        var executable = NormalizeExecutableName(request.Executable);
        if (string.IsNullOrWhiteSpace(executable))
            throw new InvalidOperationException("Executable is required.");

        var isAllowlisted = configuration.AllowedExecutables.Contains(executable);

        var workspacePath = configuration.WorkspacePath;
        var workingDirectory = ResolveWorkingDirectory(request, configuration, workspacePath);
        if (configuration.RestrictToWorkspace)
            EnsurePathInsideWorkspace(workingDirectory, workspacePath);

        var timeoutMs = ResolveTimeoutMs(request.TimeoutMs, configuration);
        var commandText = BuildCommandText(request.Executable, request.Args);

        if (configuration.RequiresApproval || !isAllowlisted)
        {
            var authorization = await _permissionRequestHandler.HandleAsync(
                new PermissionAuthorizationRequest
                {
                    Kind = "shell",
                    Message = $"允许执行命令：{commandText}",
                    SessionId = connection.SessionId,
                    ProjectId = connection.ProjectId,
                    ProjectAgentRoleId = connection.ProjectAgentRoleId,
                    Scene = connection.Scene,
                    DispatchSource = connection.DispatchSource,
                    ToolName = ShellExecToolName,
                    CommandText = commandText,
                    Warning = BuildApprovalWarning(configuration, workspacePath, request.Executable, isAllowlisted),
                    DetailsJson = JsonSerializer.Serialize(new
                    {
                        Executable = request.Executable,
                        request.Args,
                        IsAllowlisted = isAllowlisted,
                        WorkingDirectory = workingDirectory,
                        WorkspacePath = workspacePath,
                        TimeoutMs = timeoutMs
                    })
                },
                cancellationToken);

            if (authorization.Kind != PermissionAuthorizationKind.Accept)
                throw new InvalidOperationException("Shell command was rejected by permission policy.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _commandRunner.RunAsync(
                request.Executable.Trim(),
                request.Args ?? [],
                workingDirectory,
                connection.EnvironmentVariables,
                timeoutSource.Token);
            stopwatch.Stop();

            return new ShellExecResult(
                result.ExitCode,
                result.StandardOutput,
                result.StandardError,
                stopwatch.ElapsedMilliseconds,
                workingDirectory,
                workspacePath);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Shell command timed out after {timeoutMs} ms.");
        }
    }

    private async Task<ShellSystemInfoResult> GetSystemInfoAsync(
        ResolvedMcpClientConnection connection,
        ShellSystemInfoRequest request,
        CancellationToken cancellationToken)
    {
        var includeInstalledSoftware = request.IncludeInstalledSoftware;
        var maxInstalledSoftware = Math.Clamp(request.MaxInstalledSoftware, 0, 200);
        var authorization = await _permissionRequestHandler.HandleAsync(
            new PermissionAuthorizationRequest
            {
                Kind = "shell",
                Message = "允许读取系统信息：处理器、内存、磁盘、网络和软件摘要",
                SessionId = connection.SessionId,
                ProjectId = connection.ProjectId,
                ProjectAgentRoleId = connection.ProjectAgentRoleId,
                Scene = connection.Scene,
                DispatchSource = connection.DispatchSource,
                ToolName = ShellSystemInfoToolName,
                CommandText = "system.info",
                Warning = "该工具会读取当前主机的处理器、内存、磁盘、网络适配器以及已安装软件摘要，不会执行任意用户命令。",
                DetailsJson = JsonSerializer.Serialize(new
                {
                    IncludeInstalledSoftware = includeInstalledSoftware,
                    MaxInstalledSoftware = maxInstalledSoftware
                })
            },
            cancellationToken);

        if (authorization.Kind != PermissionAuthorizationKind.Accept)
            throw new InvalidOperationException("System information request was rejected by permission policy.");

        cancellationToken.ThrowIfCancellationRequested();
        return new ShellSystemInfoResult(
            CollectedAtUtc: DateTimeOffset.UtcNow,
            System: CollectSystemSummary(),
            Processor: CollectProcessorSummary(),
            Memory: CollectMemorySummary(),
            Disks: CollectDiskSummaries(),
            NetworkAdapters: CollectNetworkSummaries(),
            Software: includeInstalledSoftware
                ? CollectInstalledSoftwareSummary(maxInstalledSoftware)
                : new InstalledSoftwareSummary("skipped", 0, false, []));
    }

    private static ShellBuiltinConfiguration ParseConfiguration(ResolvedMcpClientConnection connection)
    {
        var configuration = !string.IsNullOrWhiteSpace(connection.ConnectionConfigJson)
            ? JsonSerializer.Deserialize<ShellBuiltinConfiguration>(
                connection.ConnectionConfigJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            : null;

        IEnumerable<string> executableSource = configuration?.AllowedExecutables
            ?? BuiltinShellMcpServerDefinition.DefaultAllowedExecutables;

        var allowedExecutables = executableSource
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(NormalizeExecutableName)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new ShellBuiltinConfiguration
        {
            WorkspacePath = configuration?.WorkspacePath,
            WorkingDirectory = configuration?.WorkingDirectory,
            RestrictToWorkspace = configuration?.RestrictToWorkspace ?? true,
            RequiresApproval = configuration?.RequiresApproval ?? false,
            DefaultTimeoutMs = configuration?.DefaultTimeoutMs ?? 60000,
            MaxTimeoutMs = configuration?.MaxTimeoutMs ?? 300000,
            AllowedExecutables = [.. allowedExecutables]
        };
    }

    private static string ResolveWorkingDirectory(
        ShellExecRequest request,
        ShellBuiltinConfiguration configuration,
        string? workspacePath)
    {
        var workingDirectory = !string.IsNullOrWhiteSpace(request.WorkingDirectory)
            ? request.WorkingDirectory!.Trim()
            : !string.IsNullOrWhiteSpace(configuration.WorkingDirectory)
                ? configuration.WorkingDirectory!.Trim()
                : workspacePath;

        if (string.IsNullOrWhiteSpace(workingDirectory))
            throw new InvalidOperationException("Builtin shell working directory is not configured.");

        return Path.GetFullPath(workingDirectory);
    }

    private static void EnsurePathInsideWorkspace(string workingDirectory, string? workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
            throw new InvalidOperationException("Builtin shell workspace path is required when workspace restriction is enabled.");

        var normalizedWorkspace = EnsureTrailingDirectorySeparator(Path.GetFullPath(workspacePath));
        var normalizedWorkingDirectory = EnsureTrailingDirectorySeparator(Path.GetFullPath(workingDirectory));
        if (!normalizedWorkingDirectory.StartsWith(normalizedWorkspace, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Working directory '{workingDirectory}' escapes the configured workspace '{workspacePath}'.");
        }
    }

    private static int ResolveTimeoutMs(int? requestedTimeoutMs, ShellBuiltinConfiguration configuration)
    {
        var timeoutMs = requestedTimeoutMs.GetValueOrDefault(configuration.DefaultTimeoutMs);
        if (timeoutMs <= 0)
            timeoutMs = configuration.DefaultTimeoutMs;

        return Math.Min(timeoutMs, configuration.MaxTimeoutMs);
    }

    private static string BuildApprovalWarning(
        ShellBuiltinConfiguration configuration,
        string? workspacePath,
        string executable,
        bool isAllowlisted)
    {
        var builder = new StringBuilder();
        if (!isAllowlisted)
        {
            builder.Append($"命令 '{executable}' 不在免审批白名单中，本次执行需要单独授权。");
        }

        if (configuration.RestrictToWorkspace)
        {
            if (builder.Length > 0)
                builder.Append(' ');

            builder.Append($"命令将限制在工作区内执行：{workspacePath}");
        }
        else if (builder.Length == 0)
        {
            builder.Append("命令将在主机进程内直接执行。");
        }

        return builder.ToString();
    }

    private static string NormalizeExecutableName(string? executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
            return string.Empty;

        var fileName = Path.GetFileName(executable.Trim());
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(fileName)
            : fileName;
    }

    private static string BuildCommandText(string executable, IReadOnlyList<string>? arguments)
    {
        var builder = new StringBuilder(executable);
        if (arguments == null)
            return builder.ToString();

        foreach (var argument in arguments)
        {
            builder.Append(' ');
            builder.Append(Quote(argument));
        }

        return builder.ToString();
    }

    private static string Quote(string argument)
        => string.IsNullOrWhiteSpace(argument) || argument.Contains(' ') || argument.Contains('"')
            ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : argument;

    private static string EnsureTrailingDirectorySeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static SystemSummary CollectSystemSummary()
        => new(
            HostName: Environment.MachineName,
            OperatingSystem: RuntimeInformation.OSDescription,
            OsArchitecture: RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            FrameworkDescription: RuntimeInformation.FrameworkDescription,
            TimeZone: TimeZoneInfo.Local.DisplayName,
            Uptime: TimeSpan.FromMilliseconds(Environment.TickCount64));

    private static ProcessorSummary CollectProcessorSummary()
        => new(
            Model: GetProcessorModel(),
            LogicalProcessorCount: Environment.ProcessorCount,
            Architecture: RuntimeInformation.ProcessArchitecture.ToString());

    private static MemorySummary? CollectMemorySummary()
    {
        if (OperatingSystem.IsWindows())
        {
            var status = new MemoryStatusEx();
            if (GlobalMemoryStatusEx(status))
            {
                return new MemorySummary(
                    TotalPhysicalBytes: status.TotalPhysicalBytes,
                    AvailablePhysicalBytes: status.AvailablePhysicalBytes);
            }
        }

        if (OperatingSystem.IsLinux() && File.Exists("/proc/meminfo"))
        {
            ulong? total = null;
            ulong? available = null;
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    total = ParseLinuxMeminfoBytes(line);
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                    available = ParseLinuxMeminfoBytes(line);

                if (total.HasValue && available.HasValue)
                    break;
            }

            if (total.HasValue || available.HasValue)
            {
                return new MemorySummary(
                    TotalPhysicalBytes: total,
                    AvailablePhysicalBytes: available);
            }
        }

        return null;
    }

    private static List<DiskSummary> CollectDiskSummaries()
        => DriveInfo.GetDrives()
            .OrderBy(drive => drive.Name, StringComparer.OrdinalIgnoreCase)
            .Select(drive => new DiskSummary(
                Name: drive.Name,
                Label: SafeGet(() => drive.VolumeLabel),
                DriveType: drive.DriveType.ToString(),
                Format: SafeGet(() => drive.DriveFormat),
                IsReady: drive.IsReady,
                TotalSizeBytes: drive.IsReady ? drive.TotalSize : null,
                AvailableFreeSpaceBytes: drive.IsReady ? drive.AvailableFreeSpace : null))
            .ToList();

    private static List<NetworkAdapterSummary> CollectNetworkSummaries()
        => NetworkInterface.GetAllNetworkInterfaces()
            .OrderByDescending(adapter => adapter.OperationalStatus == OperationalStatus.Up)
            .ThenBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase)
            .Select(adapter =>
            {
                var properties = adapter.GetIPProperties();
                return new NetworkAdapterSummary(
                    Name: adapter.Name,
                    Description: adapter.Description,
                    InterfaceType: adapter.NetworkInterfaceType.ToString(),
                    Status: adapter.OperationalStatus.ToString(),
                    SpeedBitsPerSecond: adapter.Speed,
                    MacAddress: FormatMacAddress(adapter.GetPhysicalAddress()),
                    IpAddresses: properties.UnicastAddresses
                        .Select(address => address.Address)
                        .Where(address => address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                        .Select(address => address.ToString())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    GatewayAddresses: properties.GatewayAddresses
                        .Select(address => address.Address)
                        .Where(address => address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                        .Select(address => address.ToString())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    DnsServers: properties.DnsAddresses
                        .Where(address => address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                        .Select(address => address.ToString())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList());
            })
            .ToList();

    private static InstalledSoftwareSummary CollectInstalledSoftwareSummary(int maxEntries)
    {
        if (!OperatingSystem.IsWindows())
            return new InstalledSoftwareSummary("unsupported", 0, false, []);

        var software = new Dictionary<string, InstalledSoftwareEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var (hive, view) in EnumerateRegistryLocations())
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstallKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstallKey == null)
                continue;

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                using var appKey = uninstallKey.OpenSubKey(subKeyName);
                var name = appKey?.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(name) || IsHiddenSystemComponent(appKey))
                    continue;

                var version = appKey?.GetValue("DisplayVersion") as string;
                var publisher = appKey?.GetValue("Publisher") as string;
                var key = $"{name}\u001f{version}\u001f{publisher}";
                software.TryAdd(
                    key,
                    new InstalledSoftwareEntry(
                        Name: name.Trim(),
                        Version: string.IsNullOrWhiteSpace(version) ? null : version.Trim(),
                        Publisher: string.IsNullOrWhiteSpace(publisher) ? null : publisher.Trim(),
                        InstallDateUtc: ParseInstallDate(appKey?.GetValue("InstallDate") as string)));
            }
        }

        var ordered = software.Values
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new InstalledSoftwareSummary(
            Source: "windows-registry",
            TotalCount: ordered.Count,
            IsTruncated: ordered.Count > maxEntries,
            Entries: ordered.Take(maxEntries).ToList());
    }

    private static IEnumerable<(RegistryHive Hive, RegistryView View)> EnumerateRegistryLocations()
    {
        yield return (RegistryHive.LocalMachine, RegistryView.Registry64);
        yield return (RegistryHive.LocalMachine, RegistryView.Registry32);
        yield return (RegistryHive.CurrentUser, RegistryView.Registry64);
        yield return (RegistryHive.CurrentUser, RegistryView.Registry32);
    }

    private static bool IsHiddenSystemComponent(RegistryKey? appKey)
        => appKey?.GetValue("SystemComponent") switch
        {
            1 => true,
            "1" => true,
            _ => false
        };

    private static DateTimeOffset? ParseInstallDate(string? installDate)
        => DateTime.TryParseExact(
            installDate,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed)
            ? new DateTimeOffset(parsed.Date)
            : null;

    private static string? GetProcessorModel()
    {
        if (OperatingSystem.IsWindows())
            return Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", null) as string;

        if (!OperatingSystem.IsLinux() || !File.Exists("/proc/cpuinfo"))
            return null;

        return File.ReadLines("/proc/cpuinfo")
            .Where(line => line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => parts[1].Trim())
            .FirstOrDefault();
    }

    private static ulong? ParseLinuxMeminfoBytes(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !ulong.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kilobytes))
            return null;

        return kilobytes * 1024;
    }

    private static string? FormatMacAddress(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 0
            ? null
            : string.Join(":", bytes.Select(item => item.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static T? SafeGet<T>(Func<T> accessor)
    {
        try
        {
            return accessor();
        }
        catch (IOException)
        {
            return default;
        }
        catch (UnauthorizedAccessException)
        {
            return default;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx status);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhysicalBytes;
        public ulong AvailablePhysicalBytes;
        public ulong TotalPageFileBytes;
        public ulong AvailablePageFileBytes;
        public ulong TotalVirtualBytes;
        public ulong AvailableVirtualBytes;
        public ulong AvailableExtendedVirtualBytes;
    }

    private sealed class ShellBuiltinConfiguration
    {
        public string? WorkspacePath { get; init; }

        public string? WorkingDirectory { get; init; }

        public bool RestrictToWorkspace { get; init; } = true;

        public bool RequiresApproval { get; init; }

        public int DefaultTimeoutMs { get; init; } = 60000;

        public int MaxTimeoutMs { get; init; } = 300000;

        public string[]? AllowedExecutables { get; init; }
    }

    public sealed record ShellExecRequest(
        string Executable,
        string[]? Args = null,
        string? WorkingDirectory = null,
        int? TimeoutMs = null);

    public sealed record ShellExecResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        long DurationMs,
        string WorkingDirectory,
        string? WorkspacePath);

    public sealed record ShellSystemInfoRequest(
        bool IncludeInstalledSoftware = true,
        int MaxInstalledSoftware = 50);

    public sealed record ShellSystemInfoResult(
        DateTimeOffset CollectedAtUtc,
        SystemSummary System,
        ProcessorSummary Processor,
        MemorySummary? Memory,
        IReadOnlyList<DiskSummary> Disks,
        IReadOnlyList<NetworkAdapterSummary> NetworkAdapters,
        InstalledSoftwareSummary Software);

    public sealed record SystemSummary(
        string HostName,
        string OperatingSystem,
        string OsArchitecture,
        string ProcessArchitecture,
        string FrameworkDescription,
        string TimeZone,
        TimeSpan Uptime);

    public sealed record ProcessorSummary(
        string? Model,
        int LogicalProcessorCount,
        string Architecture);

    public sealed record MemorySummary(
        ulong? TotalPhysicalBytes,
        ulong? AvailablePhysicalBytes);

    public sealed record DiskSummary(
        string Name,
        string? Label,
        string DriveType,
        string? Format,
        bool IsReady,
        long? TotalSizeBytes,
        long? AvailableFreeSpaceBytes);

    public sealed record NetworkAdapterSummary(
        string Name,
        string Description,
        string InterfaceType,
        string Status,
        long SpeedBitsPerSecond,
        string? MacAddress,
        IReadOnlyList<string> IpAddresses,
        IReadOnlyList<string> GatewayAddresses,
        IReadOnlyList<string> DnsServers);

    public sealed record InstalledSoftwareSummary(
        string Source,
        int TotalCount,
        bool IsTruncated,
        IReadOnlyList<InstalledSoftwareEntry> Entries);

    public sealed record InstalledSoftwareEntry(
        string Name,
        string? Version,
        string? Publisher,
        DateTimeOffset? InstallDateUtc);
}
