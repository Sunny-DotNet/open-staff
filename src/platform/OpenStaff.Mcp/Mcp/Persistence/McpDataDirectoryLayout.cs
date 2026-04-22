using Microsoft.Extensions.Options;
using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Persistence;

/// <summary>
/// zh-CN: 基于约定目录组织受管安装、缓存、manifest 和锁文件。
/// en: Organizes managed installs, caches, manifests, and lock files using the module's directory conventions.
/// </summary>
public sealed class McpDataDirectoryLayout : IMcpDataDirectoryLayout
{
    private readonly OpenStaffMcpOptions _options;

    public McpDataDirectoryLayout(IOptions<OpenStaffMcpOptions> options)
    {
        _options = options.Value;
    }

    public string GetDataRoot() => EnsureDirectory(_options.ResolveDataRootPath());

    public string GetInstallsDirectory() => EnsureDirectory(Path.Combine(GetDataRoot(), "installs"));

    public string GetMetadataDirectory() => EnsureDirectory(Path.Combine(GetDataRoot(), "metadata"));

    public string GetManifestDirectory() => EnsureDirectory(Path.Combine(GetDataRoot(), "manifests"));

    public string GetDownloadsCacheDirectory() => EnsureDirectory(Path.Combine(GetDataRoot(), "cache", "downloads"));

    public string GetExtractsCacheDirectory() => EnsureDirectory(Path.Combine(GetDataRoot(), "cache", "extracts"));

    public string GetLocksDirectory() => EnsureDirectory(Path.Combine(GetDataRoot(), "locks"));

    public string GetMetadataPath(Guid installId) => Path.Combine(GetMetadataDirectory(), $"{installId:N}.json");

    public string GetManifestPath(Guid installId) => Path.Combine(GetManifestDirectory(), $"{installId:N}.json");

    public string GetDownloadCachePath(Guid installId, string extension)
    {
        var normalizedExtension = extension.StartsWith(".") ? extension : $".{extension}";
        return Path.Combine(GetDownloadsCacheDirectory(), $"{installId:N}{normalizedExtension}");
    }

    public string GetExtractCachePath(Guid installId) => Path.Combine(GetExtractsCacheDirectory(), installId.ToString("N"));

    public string GetInstallDirectory(InstallChannel channel, string packageIdentifier, string version, string? installRootOverride = null)
    {
        var root = string.IsNullOrWhiteSpace(installRootOverride)
            ? GetInstallsDirectory()
            : Path.GetFullPath(installRootOverride);
        var segments = packageIdentifier
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(PathSegmentSanitizer.Sanitize)
            .ToList();

        if (segments.Count == 0)
            segments.Add("unknown");

        segments.Insert(0, channel.ChannelType.ToStorageValue());
        segments.Add(PathSegmentSanitizer.Sanitize(version));

        return Path.Combine([root, .. segments]);
    }

    public string GetRelativePathFromDataRoot(string fullPath)
    {
        return Path.GetRelativePath(GetDataRoot(), fullPath);
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}

internal static class PathSegmentSanitizer
{
    public static string Sanitize(string value)
    {
        var sanitizedChars = value
            .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)
            .ToArray();
        var sanitized = new string(sanitizedChars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}
