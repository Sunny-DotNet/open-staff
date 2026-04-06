using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Marketplace.Internal;

/// <summary>
/// 内置市场源 — 从本地数据库 McpServer 表查询
/// </summary>
public class InternalMcpSource : IMcpMarketplaceSource
{
    private readonly IServiceScopeFactory _scopeFactory;

    public string SourceKey => "internal";
    public string DisplayName => "内置";
    public string? IconUrl => null;

    public InternalMcpSource(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<MarketplaceSearchResult> SearchAsync(MarketplaceSearchQuery query, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var q = db.McpServers.AsNoTracking().AsQueryable();

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
            .Skip(skip).Take(query.PageSize)
            .ToListAsync(ct);

        return new MarketplaceSearchResult
        {
            TotalCount = total,
            Items = items.Select(MapToInfo).ToList()
        };
    }

    public async Task<MarketplaceServerInfo?> GetByIdAsync(string serverId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(serverId, out var id)) return null;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var server = await db.McpServers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return server == null ? null : MapToInfo(server);
    }

    private static MarketplaceServerInfo MapToInfo(McpServer s) => new()
    {
        Id = s.Id.ToString(),
        Name = s.Name,
        Description = s.Description,
        Icon = s.Icon,
        Category = s.Category,
        TransportTypes = [s.TransportType],
        Source = "internal",
        Homepage = s.Homepage,
        NpmPackage = s.NpmPackage,
        PypiPackage = s.PypiPackage,
        DefaultConfig = s.DefaultConfig,
        IsInstalled = true // 内置数据视为已安装
    };
}
