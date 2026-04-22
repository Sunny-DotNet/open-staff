using OpenStaff.Entities;
using System.Text.Json;

namespace OpenStaff.Mcp.BuiltinShell;

internal static class BuiltinShellMcpServerDefinition
{
    public const string ProviderId = "shell";
    public const string ServerName = "OpenStaff Shell";
    public const string Description = "Embedded shell automation and system-inspection tools with workspace guardrails and command allowlists.";

    public static readonly string[] DefaultAllowedExecutables =
    [
        "git",
        "dotnet",
        "pnpm",
        "node",
        "npm",
        "powershell",
        "pwsh",
        "cmd"
    ];

    public static string DefaultConfigJson => JsonSerializer.Serialize(new
    {
        transportType = McpTransportTypes.Builtin,
        builtinProvider = ProviderId,
        restrictToWorkspace = true,
        requiresApproval = true,
        defaultTimeoutMs = 60000,
        maxTimeoutMs = 300000,
        allowedExecutables = DefaultAllowedExecutables
    });

    public static McpServer CreateServer()
        => new()
        {
            Name = ServerName,
            Description = Description,
            Category = McpCategories.DevTools,
            TransportType = McpTransportTypes.Builtin,
            Mode = McpServerModes.Local,
            Source = McpSources.Builtin,
            DefaultConfig = DefaultConfigJson,
            IsEnabled = true
        };
}
