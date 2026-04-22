namespace OpenStaff.Mcp.Builtin;

/// <summary>
/// Provides embedded MCP tools for connections that stay inside the current process.
/// </summary>
public interface IBuiltinMcpToolProvider
{
    string ProviderId { get; }

    Task<IReadOnlyList<McpRuntimeToolDescriptor>> GetToolsAsync(
        ResolvedMcpClientConnection connection,
        CancellationToken cancellationToken = default);

    Task<string?> GetPreloadSkipReasonAsync(
        ResolvedMcpClientConnection connection,
        CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}
