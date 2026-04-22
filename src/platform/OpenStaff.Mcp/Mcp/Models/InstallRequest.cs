namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 表示一次安装请求。
/// en: Represents an installation request.
/// </summary>
public sealed class InstallRequest
{
    public string CatalogEntryId { get; init; } = string.Empty;

    public string SourceKey { get; init; } = string.Empty;

    public string? SelectedChannelId { get; init; }

    public string? RequestedVersion { get; init; }

    /// <summary>
    /// zh-CN: 自定义安装根目录；若为空则使用模块自己的受管目录。
    /// en: Custom install root; when omitted, the module uses its managed install root.
    /// </summary>
    public string? InstallRoot { get; init; }

    public bool OverwriteExisting { get; init; }
}
