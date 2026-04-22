using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Entities;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Repositories;

namespace OpenStaff.Marketplace.Internal;

/// <summary>
/// 内置市场源，从本地数据库中的 MCP Server 记录构建市场视图。
/// Internal marketplace source that builds marketplace views from MCP server records stored in the local database.
/// </summary>
public class InternalMcpSource : IMcpMarketplaceSource
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// 初始化内置市场源。
    /// Initializes the internal marketplace source.
    /// </summary>
    /// <param name="scopeFactory">
    /// 用于创建数据库作用域的工厂。
    /// Factory used to create database scopes.
    /// </param>
    public InternalMcpSource(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public string SourceKey => "internal";

    /// <inheritdoc />
    public string DisplayName => "内置";

    /// <inheritdoc />
    public string? IconUrl => null;

    /// <summary>
    /// 从本地 MCP Server 表中执行搜索，并将数据库记录映射为统一的市场结果模型。
    /// Executes a search against the local MCP server table and maps database records into the unified marketplace result model.
    /// </summary>
    /// <param name="query">
    /// 搜索条件，包含关键字、分类和传统分页参数。
    /// Search criteria containing keyword, category, and offset-based paging parameters.
    /// </param>
    /// <param name="ct">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// 当前页的内置市场搜索结果。
    /// Internal marketplace search results for the current page.
    /// </returns>
    public async Task<MarketplaceSearchResult> SearchAsync(MarketplaceSearchQuery query, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var mcpServers = scope.ServiceProvider.GetRequiredService<IMcpServerRepository>();

        var q = mcpServers.AsNoTracking().AsQueryable();

        // zh-CN: 搜索逻辑直接复用数据库筛选能力，只在请求包含条件时追加关键字和分类过滤。
        // en: The search logic reuses database filtering directly and only appends keyword and category predicates when the request provides them.
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            q = q.Where(s => s.Name.ToLower().Contains(kw)
                || (s.Description != null && s.Description.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(query.Category) && query.Category != "all")
            q = q.Where(s => s.Category == query.Category);

        var total = await q.CountAsync(ct);
        var skip = (query.Page - 1) * query.PageSize;
        var items = await q.OrderBy(s => s.Name)
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new MarketplaceSearchResult
        {
            TotalCount = total,
            Items = items.Select(MapToInfo).ToList()
        };
    }

    /// <summary>
    /// 按公开字符串标识读取单个内置服务器，并在查询前将其还原为本地数据库使用的 <see cref="Guid" /> 主键。
    /// Reads a single internal server by its public string identifier and converts it back to the <see cref="Guid" /> key used by the local database before querying.
    /// </summary>
    /// <param name="serverId">
    /// 对外暴露的服务器标识。
    /// Public-facing server identifier.
    /// </param>
    /// <param name="ct">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// 对应的服务器详情；标识非法或记录不存在时返回 <see langword="null" />。
    /// Matching server details, or <see langword="null" /> when the identifier is invalid or the record does not exist.
    /// </returns>
    public async Task<MarketplaceServerInfo?> GetByIdAsync(string serverId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(serverId, out var id)) return null;

        using var scope = _scopeFactory.CreateScope();
        var mcpServers = scope.ServiceProvider.GetRequiredService<IMcpServerRepository>();
        var server = await mcpServers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return server == null ? null : MapToInfo(server);
    }

    /// <summary>
    /// 将本地数据库实体转换为跨市场共享的服务器信息模型，并补齐内置源特有的安装状态语义。
    /// Converts a local database entity into the shared cross-marketplace server model and fills in installation semantics that are specific to the internal source.
    /// </summary>
    /// <param name="s">
    /// 本地数据库中的 MCP Server 实体。
    /// MCP server entity from the local database.
    /// </param>
    /// <returns>
    /// 统一后的市场服务器信息。
    /// Normalized marketplace server information.
    /// </returns>
    private static MarketplaceServerInfo MapToInfo(McpServer s) => new()
    {
        Id = s.Id.ToString(),
        Name = s.Name,
        Description = s.Description,
        Icon = s.Icon,
        Category = s.Category,

        // zh-CN: 内置表结构只存单一 TransportType，这里包装为列表以与跨源统一模型保持一致。
        // en: The internal table stores a single TransportType, so it is wrapped as a list here to match the cross-source unified model.
        TransportTypes = [s.TransportType],
        Source = "internal",
        Homepage = s.Homepage,
        NpmPackage = s.NpmPackage,
        PypiPackage = s.PypiPackage,
        DefaultConfig = s.DefaultConfig,
        IsInstalled = true
    };
}
