namespace OpenStaff.Mcp.Models;

/// <summary>
/// zh-CN: 描述一个目录来源的元数据。
/// en: Describes the metadata for a catalog source.
/// </summary>
public sealed class CatalogSource
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public int Priority { get; init; }

    public IReadOnlyList<string> Capabilities { get; init; } = [];
}
