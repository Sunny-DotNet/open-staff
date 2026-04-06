namespace OpenStaff.Application.Contracts.Marketplace.Dtos;

public class MarketplaceSourceDto
{
    public string SourceKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
}

public class MarketplaceServerDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string Category { get; set; } = "general";
    public List<string> TransportTypes { get; set; } = [];
    public string Source { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? RepositoryUrl { get; set; }
    public string? Homepage { get; set; }
    public string? NpmPackage { get; set; }
    public string? PypiPackage { get; set; }
    public string? DefaultConfig { get; set; }
    public bool IsInstalled { get; set; }
}

public class MarketplaceSearchQueryDto
{
    public string? SourceKey { get; set; }
    public string? Keyword { get; set; }
    public string? Category { get; set; }
    public string? Cursor { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class MarketplaceSearchResultDto
{
    public List<MarketplaceServerDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public string? NextCursor { get; set; }
}

public class InstallFromMarketplaceInput
{
    public string SourceKey { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
    /// <summary>可选覆盖名称</summary>
    public string? Name { get; set; }
}
