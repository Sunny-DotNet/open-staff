using System.IO.Compression;

namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: 使用平台自带 ZipFile 实现解压。
/// en: Extracts archives using the platform-provided ZipFile implementation.
/// </summary>
public sealed class ZipArchiveExtractor : IZipExtractor
{
    public Task ExtractAsync(string archivePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Directory.Exists(destinationPath))
            Directory.Delete(destinationPath, recursive: true);

        Directory.CreateDirectory(destinationPath);
        ZipFile.ExtractToDirectory(archivePath, destinationPath, overwriteFiles: true);
        return Task.CompletedTask;
    }
}
