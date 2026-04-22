using System.Text.Json;

namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: 文件系统 manifest 存储实现。
/// en: File-system implementation of the manifest store.
/// </summary>
public sealed class FileMcpManifestStore : IMcpManifestStore
{
    public async Task<McpManifest?> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<McpManifest>(stream, McpJsonSerializer.Options, cancellationToken);
    }

    public async Task WriteAsync(string path, McpManifest manifest, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, manifest, McpJsonSerializer.Options, cancellationToken);
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }
}
