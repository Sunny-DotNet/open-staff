namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: manifest 存储抽象。
/// en: Manifest-store abstraction.
/// </summary>
public interface IMcpManifestStore
{
    Task<McpManifest?> ReadAsync(string path, CancellationToken cancellationToken = default);

    Task WriteAsync(string path, McpManifest manifest, CancellationToken cancellationToken = default);

    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
