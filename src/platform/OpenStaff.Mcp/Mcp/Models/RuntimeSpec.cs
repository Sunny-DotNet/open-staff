namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示最终可直接运行的运行时规格。
/// en: Represents the final runtime specification that can be executed directly.
/// </summary>
public sealed class RuntimeSpec
{
    public McpTransportType TransportType { get; init; }

    public string? Url { get; init; }

    public IReadOnlyDictionary<string, string?> Headers { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public string? Command { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public string? WorkingDirectory { get; init; }
}
