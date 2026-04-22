using System.Text.Json.Nodes;

namespace OpenStaff.Application.McpServers.Services;

/// <summary>
/// Provides context-aware default MCP runtime parameters so host-scoped drafts and project-scoped bindings
/// share the same default-resolution rules.
/// </summary>
public sealed class McpRuntimeParameterDefaultsService
{
    private const string FilesystemWorkspacePathKey = "workspacePath";
    private const string FilesystemWorkingDirectoryKey = "workingDirectory";
    private const string ShellWorkspacePathKey = "workspacePath";
    private const string ShellWorkingDirectoryKey = "workingDirectory";
    private static readonly string FilesystemTempWorkspacePath = Path.Combine(
        Path.GetTempPath(),
        "openstaff-mcp",
        "filesystem");
    private static readonly string ShellTempWorkspacePath = Path.Combine(
        Path.GetTempPath(),
        "openstaff-mcp",
        "shell");

    public JsonObject CreateHostDefaults(McpServer server)
    {
        var runtimeParameters = new JsonObject();
        if (IsFilesystemServer(server))
            SetFilesystemWorkspace(runtimeParameters, GetFilesystemTempWorkspacePath());
        if (IsBuiltinShellServer(server))
            SetShellWorkspace(runtimeParameters, GetShellTempWorkspacePath());

        return runtimeParameters;
    }

    public JsonObject CreateProjectDefaults(McpServer server, string? projectWorkspacePath)
    {
        var runtimeParameters = new JsonObject();
        if (IsFilesystemServer(server))
            SetFilesystemWorkspace(runtimeParameters, ResolveFilesystemWorkspace(projectWorkspacePath));
        if (IsBuiltinShellServer(server))
            SetShellWorkspace(runtimeParameters, ResolveShellWorkspace(projectWorkspacePath));

        return runtimeParameters;
    }

    public JsonObject RewriteForProjectContext(McpServer server, JsonObject sourceRuntimeParameters, string? projectWorkspacePath)
    {
        var runtimeParameters = JsonNode.Parse(sourceRuntimeParameters.ToJsonString()) as JsonObject ?? new JsonObject();
        if (IsFilesystemServer(server))
            SetFilesystemWorkspace(runtimeParameters, ResolveFilesystemWorkspace(projectWorkspacePath));
        if (IsBuiltinShellServer(server))
            SetShellWorkspace(runtimeParameters, ResolveShellWorkspace(projectWorkspacePath));

        return runtimeParameters;
    }

    public string GetFilesystemTempWorkspacePath()
    {
        Directory.CreateDirectory(FilesystemTempWorkspacePath);
        return FilesystemTempWorkspacePath;
    }

    public string GetShellTempWorkspacePath()
    {
        Directory.CreateDirectory(ShellTempWorkspacePath);
        return ShellTempWorkspacePath;
    }

    private string ResolveFilesystemWorkspace(string? projectWorkspacePath)
        => string.IsNullOrWhiteSpace(projectWorkspacePath)
            ? GetFilesystemTempWorkspacePath()
            : projectWorkspacePath.Trim();

    private string ResolveShellWorkspace(string? projectWorkspacePath)
        => string.IsNullOrWhiteSpace(projectWorkspacePath)
            ? GetShellTempWorkspacePath()
            : projectWorkspacePath.Trim();

    private static void SetFilesystemWorkspace(JsonObject runtimeParameters, string workspacePath)
    {
        runtimeParameters[FilesystemWorkspacePathKey] = workspacePath;
        runtimeParameters[FilesystemWorkingDirectoryKey] = workspacePath;
        runtimeParameters.Remove("workspace");
        runtimeParameters.Remove("workspaces");
    }

    private static void SetShellWorkspace(JsonObject runtimeParameters, string workspacePath)
    {
        runtimeParameters[ShellWorkspacePathKey] = workspacePath;
        runtimeParameters[ShellWorkingDirectoryKey] = workspacePath;
    }

    private static bool IsFilesystemServer(McpServer server)
        => string.Equals(server.Name, "Filesystem", StringComparison.OrdinalIgnoreCase)
            || string.Equals(server.NpmPackage, "@modelcontextprotocol/server-filesystem", StringComparison.OrdinalIgnoreCase);

    private static bool IsBuiltinShellServer(McpServer server)
        => string.Equals(server.TransportType, McpTransportTypes.Builtin, StringComparison.OrdinalIgnoreCase)
            && string.Equals(server.Name, "OpenStaff Shell", StringComparison.OrdinalIgnoreCase);
}
