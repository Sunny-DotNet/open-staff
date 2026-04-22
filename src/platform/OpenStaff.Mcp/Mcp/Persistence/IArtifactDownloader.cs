namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: 负责下载安装产物。
/// en: Downloads install artifacts.
/// </summary>
public interface IArtifactDownloader
{
    Task DownloadAsync(Uri artifactUri, string destinationPath, string? checksum = null, CancellationToken cancellationToken = default);
}
