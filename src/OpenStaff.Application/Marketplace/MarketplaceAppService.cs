using Microsoft.EntityFrameworkCore;
using OpenStaff.Application.Contracts.Marketplace;
using OpenStaff.Application.Contracts.Marketplace.Dtos;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;
using OpenStaff.Marketplace;

namespace OpenStaff.Application.Marketplace;

public class MarketplaceAppService : IMarketplaceAppService
{
    private readonly IMarketplaceSourceFactory _sourceFactory;
    private readonly AppDbContext _db;

    public MarketplaceAppService(IMarketplaceSourceFactory sourceFactory, AppDbContext db)
    {
        _sourceFactory = sourceFactory;
        _db = db;
    }

    public Task<List<MarketplaceSourceDto>> GetSourcesAsync(CancellationToken ct = default)
    {
        var sources = _sourceFactory.GetAllSources()
            .Select(s => new MarketplaceSourceDto
            {
                SourceKey = s.SourceKey,
                DisplayName = s.DisplayName,
                IconUrl = s.IconUrl
            })
            .ToList();

        return Task.FromResult(sources);
    }

    public async Task<MarketplaceSearchResultDto> SearchAsync(MarketplaceSearchQueryDto query, CancellationToken ct = default)
    {
        var sourceKey = query.SourceKey ?? "internal";
        var source = _sourceFactory.GetSource(sourceKey)
            ?? throw new KeyNotFoundException($"市场源 '{sourceKey}' 未注册");

        var searchQuery = new MarketplaceSearchQuery
        {
            Keyword = query.Keyword,
            Category = query.Category,
            Cursor = query.Cursor,
            Page = query.Page,
            PageSize = query.PageSize
        };

        var result = await source.SearchAsync(searchQuery, ct);

        // 标记已安装状态（对外部源的结果检查本地 DB）
        if (sourceKey != "internal" && result.Items.Count > 0)
        {
            var names = result.Items.Select(i => i.Name).ToHashSet();
            var installed = await _db.McpServers.AsNoTracking()
                .Where(s => names.Contains(s.Name))
                .Select(s => s.Name)
                .ToListAsync(ct);
            var installedSet = installed.ToHashSet();

            foreach (var item in result.Items)
            {
                item.IsInstalled = installedSet.Contains(item.Name);
            }
        }

        return new MarketplaceSearchResultDto
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            NextCursor = result.NextCursor
        };
    }

    public async Task<MarketplaceServerDto> InstallAsync(InstallFromMarketplaceInput input, CancellationToken ct = default)
    {
        var source = _sourceFactory.GetSource(input.SourceKey)
            ?? throw new KeyNotFoundException($"市场源 '{input.SourceKey}' 未注册");

        var serverInfo = await source.GetByIdAsync(input.ServerId, ct)
            ?? throw new KeyNotFoundException($"MCP Server '{input.ServerId}' 在 '{input.SourceKey}' 中未找到");

        // 检查是否已安装
        var existing = await _db.McpServers.FirstOrDefaultAsync(
            s => s.Name == serverInfo.Name, ct);
        if (existing != null)
            throw new InvalidOperationException($"'{serverInfo.Name}' 已安装");

        var entity = new McpServer
        {
            Name = input.Name ?? serverInfo.Name,
            Description = serverInfo.Description,
            Icon = serverInfo.Icon,
            Category = serverInfo.Category,
            TransportType = serverInfo.TransportTypes.FirstOrDefault() ?? "stdio",
            Source = McpSources.Marketplace,
            Homepage = serverInfo.Homepage,
            NpmPackage = serverInfo.NpmPackage,
            PypiPackage = serverInfo.PypiPackage,
            DefaultConfig = serverInfo.DefaultConfig,
            MarketplaceUrl = serverInfo.RepositoryUrl
        };

        _db.McpServers.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dto = MapToDto(serverInfo);
        dto.IsInstalled = true;
        return dto;
    }

    private static MarketplaceServerDto MapToDto(MarketplaceServerInfo info) => new()
    {
        Id = info.Id,
        Name = info.Name,
        Description = info.Description,
        Icon = info.Icon,
        Category = info.Category,
        TransportTypes = info.TransportTypes,
        Source = info.Source,
        Version = info.Version,
        RepositoryUrl = info.RepositoryUrl,
        Homepage = info.Homepage,
        NpmPackage = info.NpmPackage,
        PypiPackage = info.PypiPackage,
        DefaultConfig = info.DefaultConfig,
        IsInstalled = info.IsInstalled
    };
}
