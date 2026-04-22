using OpenStaff.Mcp.Exceptions;
using OpenStaff.Mcp.Models;
using OpenStaff.Mcp.Persistence;
using OpenStaff.Mcp.Sources;

namespace OpenStaff.Mcp.Services;

/// <summary>
/// zh-CN: 聚合多个目录来源并叠加本地安装状态。
/// en: Aggregates multiple catalog sources and overlays local installation state.
/// </summary>
public sealed class McpCatalogService : IMcpCatalogService
{
    private readonly IReadOnlyList<IMcpCatalogSource> _sources;
    private readonly IInstalledMcpMetadataStore _metadataStore;

    public McpCatalogService(IEnumerable<IMcpCatalogSource> sources, IInstalledMcpMetadataStore metadataStore)
    {
        _sources = sources
            .OrderBy(source => source.Priority)
            .ToList();
        _metadataStore = metadataStore;
    }

    public async Task<CatalogSearchResult> SearchCatalogAsync(CatalogSearchQuery query, CancellationToken cancellationToken = default)
    {
        var sources = ResolveSources(query.SourceKey);
        var queryWithoutPaging = query.ToUnpagedQuery();
        var entries = new List<CatalogEntry>();

        foreach (var source in sources)
        {
            var sourceEntries = await source.SearchAsync(queryWithoutPaging, cancellationToken);
            entries.AddRange(sourceEntries);
        }

        var installedIndex = (await _metadataStore.ListAsync(cancellationToken))
            .GroupBy(item => $"{item.SourceKey}|{item.CatalogEntryId}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt).First(), StringComparer.OrdinalIgnoreCase);

        var filtered = entries
            .Where(entry => MatchesKeyword(entry, query.Keyword))
            .Where(entry => MatchesCategory(entry, query.Category))
            .Where(entry => !query.TransportType.HasValue || entry.TransportTypes.Contains(query.TransportType.Value))
            .Select(entry => OverlayInstallationState(entry, installedIndex))
            .ToList();

        var pageSize = Math.Clamp(query.PageSize ?? 50, 1, 200);
        var offset = ResolveOffset(query, pageSize);
        var page = filtered
            .Skip(offset)
            .Take(pageSize)
            .ToList();

        return new CatalogSearchResult
        {
            Items = page,
            TotalCount = filtered.Count,
            NextCursor = offset + page.Count < filtered.Count ? (offset + page.Count).ToString() : null
        };
    }

    public async Task<CatalogEntry> GetCatalogEntryAsync(string sourceKey, string entryId, CancellationToken cancellationToken = default)
    {
        var source = ResolveSources(sourceKey).SingleOrDefault()
            ?? throw new CatalogEntryNotFoundException(sourceKey, entryId);
        var entry = await source.GetByIdAsync(entryId, cancellationToken)
            ?? throw new CatalogEntryNotFoundException(sourceKey, entryId);
        var installed = await _metadataStore.GetByCatalogEntryAsync(sourceKey, entryId, cancellationToken);
        var installedIndex = installed != null
            ? new Dictionary<string, InstalledMcp>(StringComparer.OrdinalIgnoreCase)
            {
                [$"{sourceKey}|{entryId}"] = installed
            }
            : new Dictionary<string, InstalledMcp>(StringComparer.OrdinalIgnoreCase);
        return OverlayInstallationState(entry, installedIndex);
    }

    private IReadOnlyList<IMcpCatalogSource> ResolveSources(string? sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return _sources;

        return _sources
            .Where(source => string.Equals(source.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static CatalogEntry OverlayInstallationState(
        CatalogEntry entry,
        IReadOnlyDictionary<string, InstalledMcp> installedIndex)
    {
        var key = $"{entry.SourceKey}|{entry.EntryId}";
        if (!installedIndex.TryGetValue(key, out var installed))
            return entry;

        return new CatalogEntry
        {
            EntryId = entry.EntryId,
            SourceKey = entry.SourceKey,
            Name = entry.Name,
            DisplayName = entry.DisplayName,
            Description = entry.Description,
            Category = entry.Category,
            Version = entry.Version,
            Homepage = entry.Homepage,
            RepositoryUrl = entry.RepositoryUrl,
            TransportTypes = entry.TransportTypes,
            InstallChannels = entry.InstallChannels,
            IsInstalled = true,
            InstalledState = installed.InstallState,
            InstalledVersion = installed.Version
        };
    }

    private static bool MatchesKeyword(CatalogEntry entry, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return true;

        return entry.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
               || entry.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
               || (entry.Description?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool MatchesCategory(CatalogEntry entry, string? category)
    {
        return string.IsNullOrWhiteSpace(category)
               || string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveOffset(CatalogSearchQuery query, int pageSize)
    {
        if (int.TryParse(query.Cursor, out var cursorOffset) && cursorOffset >= 0)
            return cursorOffset;

        var page = Math.Max(query.Page ?? 1, 1);
        return (page - 1) * pageSize;
    }
}
