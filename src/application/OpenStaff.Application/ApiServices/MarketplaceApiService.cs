namespace OpenStaff.ApiServices;
/// <summary>
/// Legacy marketplace facade that delegates to the unified MCP application service.
/// </summary>
public class MarketplaceApiService : ApiServiceBase, IMarketplaceApiService
{
    private readonly IMcpApiService _mcpApiService;

    public MarketplaceApiService(
        IMcpApiService mcpApiService,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _mcpApiService = mcpApiService;
    }

    public async Task<List<MarketplaceSourceDto>> GetSourcesAsync(CancellationToken ct = default)
    {
        var sources = await _mcpApiService.GetSourcesAsync(ct);
        return sources.Select(source => new MarketplaceSourceDto
        {
            SourceKey = source.SourceKey,
            DisplayName = source.DisplayName
        }).ToList();
    }

    public async Task<MarketplaceSearchResultDto> SearchAsync(MarketplaceSearchQueryDto query, CancellationToken ct = default)
    {
        var result = await _mcpApiService.SearchCatalogAsync(new McpCatalogSearchQueryDto
        {
            SourceKey = query.SourceKey,
            Keyword = query.Keyword,
            Category = query.Category,
            Cursor = query.Cursor,
            Page = query.Page,
            PageSize = query.PageSize
        }, ct);

        return new MarketplaceSearchResultDto
        {
            Items = result.Items.Select(MapCatalogEntry).ToList(),
            TotalCount = result.TotalCount,
            NextCursor = result.NextCursor
        };
    }

    public async Task<MarketplaceServerDto> InstallAsync(InstallFromMarketplaceInput input, CancellationToken ct = default)
    {
        var server = await _mcpApiService.InstallAsync(new InstallMcpServerInput
        {
            SourceKey = input.SourceKey,
            CatalogEntryId = input.ServerId,
            Name = input.Name
        }, ct);

        return new MarketplaceServerDto
        {
            Id = server.CatalogEntryId ?? server.Id.ToString("N"),
            Name = server.Name,
            Description = server.Description,
            Icon = server.Icon,
            Category = server.Category,
            TransportTypes = [server.TransportType],
            Source = server.InstallSourceKey ?? server.Source,
            Version = server.InstalledVersion,
            RepositoryUrl = server.Homepage,
            Homepage = server.Homepage,
            NpmPackage = server.NpmPackage,
            PypiPackage = server.PypiPackage,
            DefaultConfig = server.TemplateJson,
            IsInstalled = server.IsManagedInstall
        };
    }

    private static MarketplaceServerDto MapCatalogEntry(McpCatalogEntryDto entry) => new()
    {
        Id = entry.EntryId,
        Name = entry.DisplayName,
        Description = entry.Description,
        Category = entry.Category ?? "general",
        TransportTypes = entry.TransportTypes,
        Source = entry.SourceKey,
        Version = entry.Version,
        RepositoryUrl = entry.RepositoryUrl,
        Homepage = entry.Homepage,
        NpmPackage = entry.InstallChannels
            .FirstOrDefault(channel => string.Equals(channel.ChannelType, "npm", StringComparison.OrdinalIgnoreCase))
            ?.PackageIdentifier,
        PypiPackage = entry.InstallChannels
            .FirstOrDefault(channel => string.Equals(channel.ChannelType, "pypi", StringComparison.OrdinalIgnoreCase))
            ?.PackageIdentifier,
        IsInstalled = entry.IsInstalled
    };
}




