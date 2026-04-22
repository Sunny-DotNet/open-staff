namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: zip 解压抽象。
/// en: Abstraction for zip extraction.
/// </summary>
public interface IZipExtractor
{
    Task ExtractAsync(string archivePath, string destinationPath, CancellationToken cancellationToken = default);
}
