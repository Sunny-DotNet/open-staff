using Microsoft.Extensions.Logging;

namespace OpenStaff.Marketplace.Registry;

/// <summary>
/// 官方 Registry 市场源 — 对接 registry.modelcontextprotocol.io
/// </summary>
public class RegistryMcpSource : IMcpMarketplaceSource
{
    private readonly RegistryApiClient _apiClient;
    private readonly ILogger<RegistryMcpSource> _logger;

    public string SourceKey => "registry";
    public string DisplayName => "官方 Registry";
    public string? IconUrl => null;

    public RegistryMcpSource(RegistryApiClient apiClient, ILogger<RegistryMcpSource> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<MarketplaceSearchResult> SearchAsync(MarketplaceSearchQuery query, CancellationToken ct = default)
    {
        var response = await _apiClient.ListServersAsync(
            cursor: query.Cursor,
            limit: query.PageSize,
            keyword: query.Keyword,
            ct: ct);

        // 只保留 isLatest 版本，避免重复
        var latestServers = response.Servers
            .Where(e => e.Meta?.Official?.IsLatest != false)
            .ToList();

        // 客户端侧关键词过滤（Registry API 暂不支持 keyword 参数）
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            latestServers = latestServers
                .Where(e => e.Server.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)
                    || (e.Server.Description?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true))
                .ToList();
        }

        var items = latestServers.Select(MapToInfo).ToList();

        return new MarketplaceSearchResult
        {
            Items = items,
            TotalCount = items.Count,
            NextCursor = response.Metadata.NextCursor
        };
    }

    public async Task<MarketplaceServerInfo?> GetByIdAsync(string serverId, CancellationToken ct = default)
    {
        // Registry 没有 GetById 端点，遍历搜索
        // 实际使用中一般不需要此方法（从列表点击安装）
        _logger.LogDebug("GetByIdAsync not efficiently supported by Registry API for {ServerId}", serverId);
        return null;
    }

    private static MarketplaceServerInfo MapToInfo(RegistryServerEntry entry)
    {
        var s = entry.Server;
        var transportTypes = s.Remotes?.Select(r => r.Type).Distinct().ToList() ?? [];

        // 从 packages 推断传输类型和包信息
        string? npmPackage = null;
        string? pypiPackage = null;

        if (s.Packages != null)
        {
            foreach (var pkg in s.Packages)
            {
                if (pkg.Transport != null && !string.IsNullOrEmpty(pkg.Transport.Type)
                    && !transportTypes.Contains(pkg.Transport.Type))
                    transportTypes.Add(pkg.Transport.Type);

                if (pkg.RegistryType == "npm")
                    npmPackage ??= pkg.Identifier;
                else if (pkg.RegistryType == "pypi")
                    pypiPackage ??= pkg.Identifier;

                // npm/pypi 包暗示支持 stdio
                if (pkg.RegistryType is "npm" or "pypi" && !transportTypes.Contains("stdio"))
                    transportTypes.Add("stdio");
            }
        }

        return new MarketplaceServerInfo
        {
            Id = $"{s.Name}:{s.Version}",
            Name = s.Name,
            Description = s.Description,
            Version = s.Version,
            TransportTypes = transportTypes.Count > 0 ? transportTypes : ["stdio"],
            Source = "registry",
            RepositoryUrl = s.Repository?.Url,
            Homepage = s.WebsiteUrl,
            NpmPackage = npmPackage,
            PypiPackage = pypiPackage,
            Remotes = s.Remotes?.Select(r => new RemoteEndpoint { Type = r.Type, Url = r.Url }).ToList() ?? []
        };
    }
}
