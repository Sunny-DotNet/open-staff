using Microsoft.Extensions.Logging;

namespace OpenStaff.Marketplace.Registry;

/// <summary>
/// 官方 Registry 市场源，对接 <c>registry.modelcontextprotocol.io</c>。
/// Official registry marketplace source that integrates with <c>registry.modelcontextprotocol.io</c>.
/// </summary>
public class RegistryMcpSource : IMcpMarketplaceSource
{
    private readonly RegistryApiClient _apiClient;
    private readonly ILogger<RegistryMcpSource> _logger;

    /// <summary>
    /// 初始化 Registry 市场源。
    /// Initializes the registry marketplace source.
    /// </summary>
    /// <param name="apiClient">
    /// Registry API 客户端。
    /// Registry API client.
    /// </param>
    /// <param name="logger">
    /// 市场源日志记录器。
    /// Marketplace source logger.
    /// </param>
    public RegistryMcpSource(RegistryApiClient apiClient, ILogger<RegistryMcpSource> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public string SourceKey => "registry";

    /// <inheritdoc />
    public string DisplayName => "官方 Registry";

    /// <inheritdoc />
    public string? IconUrl => null;

    /// <summary>
    /// 从官方 Registry 拉取一页服务条目，并将远程响应折叠为统一的市场搜索结果。
    /// Pulls a page of server entries from the official registry and folds the remote response into the unified marketplace search result.
    /// </summary>
    /// <param name="query">
    /// 搜索条件，包含远程游标与本地补充过滤所需的关键字参数。
    /// Search criteria containing the remote cursor and keyword values needed for local fallback filtering.
    /// </param>
    /// <param name="ct">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// 当前页 Registry 搜索结果，并保留下一页游标供继续翻页。
    /// Registry search results for the current page with the next cursor preserved for continued paging.
    /// </returns>
    public async Task<MarketplaceSearchResult> SearchAsync(MarketplaceSearchQuery query, CancellationToken ct = default)
    {
        var response = await _apiClient.ListServersAsync(
            cursor: query.Cursor,
            limit: query.PageSize,
            keyword: query.Keyword,
            ct: ct);

        // zh-CN: Registry 可能返回同一服务的多个版本，这里优先保留最新版本，避免 UI 中出现重复项。
        // en: The registry can return multiple versions of the same server, so keep only the latest entries to avoid duplicates in the UI.
        var latestServers = response.Servers
            .Where(e => e.Meta?.Official?.IsLatest != false)
            .ToList();

        // zh-CN: 在官方 API 尚未支持关键字参数前，继续在客户端侧做名称与描述过滤。
        // en: Until the official API supports keyword parameters, continue filtering by name and description on the client side.
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

    /// <summary>
    /// 通过有限次分页遍历模拟 Registry 的按标识查找，因为官方接口当前不提供直接的详情读取端点。
    /// Simulates registry lookup by identifier through a bounded pagination walk because the official API does not currently expose a direct detail endpoint.
    /// </summary>
    /// <param name="serverId">
    /// 统一模型中的服务器标识，格式通常为 <c>name:version</c>。
    /// Server identifier from the unified model, typically in <c>name:version</c> format.
    /// </param>
    /// <param name="ct">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// 匹配的服务器详情；超过分页上限或未找到时返回 <see langword="null" />。
    /// Matching server details, or <see langword="null" /> when the pagination limit is exceeded or no entry is found.
    /// </returns>
    public async Task<MarketplaceServerInfo?> GetByIdAsync(string serverId, CancellationToken ct = default)
    {
        // zh-CN: Registry 没有按 ID 直查的接口，因此通过分页遍历并复用 nextCursor 定位目标条目。
        // en: The registry has no direct lookup-by-id endpoint, so paginate through the catalog and use nextCursor to locate the target entry.
        string? cursor = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await _apiClient.ListServersAsync(cursor: cursor, limit: 100, ct: ct);
            if (response.Servers.Count == 0) break;

            foreach (var entry in response.Servers)
            {
                var info = MapToInfo(entry);
                if (info.Id == serverId)
                    return info;
            }

            cursor = response.Metadata.NextCursor;
            if (string.IsNullOrEmpty(cursor)) break;
        }

        _logger.LogWarning("Registry server {ServerId} not found after pagination", serverId);
        return null;
    }

    /// <summary>
    /// 将 Registry 响应条目规范化为统一市场模型，并合并远程端点与包元数据推断安装入口和传输类型。
    /// Normalizes a registry response entry into the unified marketplace model and merges remote endpoint plus package metadata to infer install channels and transport types.
    /// </summary>
    /// <param name="entry">
    /// Registry 返回的原始服务条目。
    /// Raw server entry returned by the registry.
    /// </param>
    /// <returns>
    /// 统一后的市场服务器信息。
    /// Normalized marketplace server information.
    /// </returns>
    private static MarketplaceServerInfo MapToInfo(RegistryServerEntry entry)
    {
        var s = entry.Server;
        var transportTypes = s.Remotes?.Select(r => r.Type).Distinct().ToList() ?? [];

        string? npmPackage = null;
        string? pypiPackage = null;

        // zh-CN: Registry 包信息既能补充传输类型，也能推断 npm/pypi 安装入口，因此需要统一折叠进跨源模型。
        // en: Package metadata can enrich transport types and reveal npm or PyPI installation entry points, so fold it into the unified cross-source model.
        if (s.Packages != null)
        {
            foreach (var pkg in s.Packages)
            {
                if (pkg.Transport != null && !string.IsNullOrEmpty(pkg.Transport.Type)
                    && !transportTypes.Contains(pkg.Transport.Type))
                {
                    transportTypes.Add(pkg.Transport.Type);
                }

                if (pkg.RegistryType == "npm")
                    npmPackage ??= pkg.Identifier;
                else if (pkg.RegistryType == "pypi")
                    pypiPackage ??= pkg.Identifier;

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
