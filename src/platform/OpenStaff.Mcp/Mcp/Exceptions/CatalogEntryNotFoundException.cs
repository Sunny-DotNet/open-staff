namespace OpenStaff.Mcp.Exceptions;

/// <summary>
/// zh-CN: 当目录条目不存在时抛出。
/// en: Thrown when a catalog entry cannot be found.
/// </summary>
public sealed class CatalogEntryNotFoundException : McpException
{
    public CatalogEntryNotFoundException(string sourceKey, string entryId)
        : base("CatalogEntryNotFound", $"Catalog entry '{entryId}' was not found in source '{sourceKey}'.")
    {
    }
}
