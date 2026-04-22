using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: 持久化到 manifest 中的运行时规格，允许将安装目录相关路径延后到解析阶段再还原为绝对路径。
/// en: Runtime specification persisted in the manifest; install-directory-relative paths are expanded to absolute paths during resolution.
/// </summary>
public sealed class PersistedRuntimeSpec
{
    public McpTransportType TransportType { get; init; }

    public string? Url { get; init; }

    public IReadOnlyDictionary<string, string?> Headers { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public string? Command { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public string? WorkingDirectory { get; init; }

    public bool CommandRelativeToInstallDirectory { get; init; }

    public bool WorkingDirectoryRelativeToInstallDirectory { get; init; }

    public IReadOnlyList<int> ArgumentsRelativeToInstallDirectory { get; init; } = [];
}
