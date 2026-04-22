using System.Text.Json;
using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: 以每条记录一个 JSON 文件的方式持久化安装元数据，保持简单且便于手工诊断。
/// en: Persists install metadata as one JSON file per record, keeping storage simple and easy to inspect manually.
/// </summary>
public sealed class FileInstalledMcpMetadataStore : IInstalledMcpMetadataStore
{
    private readonly IMcpDataDirectoryLayout _layout;

    public FileInstalledMcpMetadataStore(IMcpDataDirectoryLayout layout)
    {
        _layout = layout;
    }

    public async Task<IReadOnlyList<InstalledMcp>> ListAsync(CancellationToken cancellationToken = default)
    {
        var directory = _layout.GetMetadataDirectory();
        var files = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            : [];

        var installed = new List<InstalledMcp>(files.Length);
        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file);
            var item = await JsonSerializer.DeserializeAsync<InstalledMcp>(stream, McpJsonSerializer.Options, cancellationToken)
                ?? throw new InvalidDataException($"Metadata file '{file}' is empty or invalid.");
            installed.Add(item);
        }

        return installed
            .OrderByDescending(item => item.UpdatedAt)
            .ToList();
    }

    public async Task<InstalledMcp?> GetAsync(Guid installId, CancellationToken cancellationToken = default)
    {
        var path = _layout.GetMetadataPath(installId);
        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<InstalledMcp>(stream, McpJsonSerializer.Options, cancellationToken);
    }

    public async Task<InstalledMcp?> GetByCatalogEntryAsync(string sourceKey, string catalogEntryId, CancellationToken cancellationToken = default)
    {
        var installed = await ListAsync(cancellationToken);
        return installed.FirstOrDefault(item =>
            string.Equals(item.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.CatalogEntryId, catalogEntryId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task UpsertAsync(InstalledMcp installedMcp, CancellationToken cancellationToken = default)
    {
        var path = _layout.GetMetadataPath(installedMcp.InstallId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, installedMcp, McpJsonSerializer.Options, cancellationToken);
    }

    public Task DeleteAsync(Guid installId, CancellationToken cancellationToken = default)
    {
        var path = _layout.GetMetadataPath(installId);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }
}
