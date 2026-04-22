using OpenStaff.Skills.Models;
using OpenStaff.Skills.Sources;

namespace OpenStaff.Skills.Services;

/// <summary>
/// Default skill catalog service.
/// </summary>
public sealed class SkillCatalogService : ISkillCatalogService
{
    private readonly ISkillCatalogSource _catalogSource;

    public SkillCatalogService(ISkillCatalogSource catalogSource)
    {
        _catalogSource = catalogSource;
    }

    /// <inheritdoc />
    public async Task<SkillCatalogSearchResult> SearchAsync(
        SkillCatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IEnumerable<SkillCatalogEntry> items = await _catalogSource.GetAllAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            items = items.Where(item =>
                item.SkillId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.Owner.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.Repo.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(item.Description) && item.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.Owner))
        {
            var owner = query.Owner.Trim();
            items = items.Where(item => string.Equals(item.Owner, owner, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Repo))
        {
            var repo = query.Repo.Trim();
            items = items.Where(item => string.Equals(item.Repo, repo, StringComparison.OrdinalIgnoreCase));
        }

        items = items
            .OrderByDescending(item => item.Installs)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalCount = items.Count();
        var pageItems = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SkillCatalogSearchResult
        {
            Items = pageItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public Task<SkillCatalogEntry?> GetAsync(
        string owner,
        string repo,
        string skillId,
        CancellationToken cancellationToken = default)
        => _catalogSource.GetAsync(owner, repo, skillId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<SkillCatalogSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SkillCatalogSource>>(
        [
            new SkillCatalogSource
            {
                Key = _catalogSource.SourceKey,
                DisplayName = _catalogSource.DisplayName
            }
        ]);
}
