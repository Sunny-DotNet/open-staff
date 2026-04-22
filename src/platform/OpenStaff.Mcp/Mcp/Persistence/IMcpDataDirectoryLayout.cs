using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: 负责计算 OpenStaff.Mcp 的受管目录结构。
/// en: Computes the managed directory structure for OpenStaff.Mcp.
/// </summary>
public interface IMcpDataDirectoryLayout
{
    string GetDataRoot();

    string GetInstallsDirectory();

    string GetMetadataDirectory();

    string GetManifestDirectory();

    string GetDownloadsCacheDirectory();

    string GetExtractsCacheDirectory();

    string GetLocksDirectory();

    string GetMetadataPath(Guid installId);

    string GetManifestPath(Guid installId);

    string GetDownloadCachePath(Guid installId, string extension);

    string GetExtractCachePath(Guid installId);

    string GetInstallDirectory(InstallChannel channel, string packageIdentifier, string version, string? installRootOverride = null);

    string GetRelativePathFromDataRoot(string fullPath);
}
