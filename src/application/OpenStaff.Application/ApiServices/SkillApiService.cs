using OpenStaff.Application.Skills.Services;
using PlatformSkillCatalogEntry = OpenStaff.Skills.Models.SkillCatalogEntry;
using PlatformSkillCatalogQuery = OpenStaff.Skills.Models.SkillCatalogQuery;

namespace OpenStaff.ApiServices;
/// <summary>
/// Lightweight application service that projects the OpenStaff.Skills module to HTTP-facing DTOs.
/// </summary>
public sealed class SkillApiService : ApiServiceBase, ISkillApiService
{
    private readonly ISkillCatalogService _catalogService;
    private readonly IManagedSkillStore _managedSkillStore;
    private readonly IAgentRoleRepository _agentRoles;
    private readonly IAgentRoleSkillBindingRepository _agentRoleSkillBindings;
    private readonly IRepositoryContext _repositoryContext;

    /// <summary>
    /// Initializes the service.
    /// </summary>
    public SkillApiService(
        ISkillCatalogService catalogService,
        IManagedSkillStore managedSkillStore,
        IAgentRoleRepository agentRoles,
        IAgentRoleSkillBindingRepository agentRoleSkillBindings,
        IRepositoryContext repositoryContext,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _catalogService = catalogService;
        _managedSkillStore = managedSkillStore;
        _agentRoles = agentRoles;
        _agentRoleSkillBindings = agentRoleSkillBindings;
        _repositoryContext = repositoryContext;
    }

    /// <inheritdoc />
    public async Task<SkillCatalogPageDto> SearchCatalogAsync(SkillCatalogQueryInput input, CancellationToken ct = default)
    {
        var installedSet = await LoadInstalledIdentitySetAsync(ct);
        var result = await _catalogService.SearchAsync(
            new PlatformSkillCatalogQuery
            {
                Keyword = Normalize(input.Query),
                Owner = Normalize(input.Owner),
                Repo = Normalize(input.Repo),
                Page = Math.Max(1, input.Page),
                PageSize = Math.Clamp(input.PageSize, 1, 100)
            },
            ct);

        return new SkillCatalogPageDto
        {
            Items = result.Items
                .Select(item => ToCatalogItemDto(
                    item,
                    installedSet.Contains(BuildCatalogIdentity(item.Owner, item.Repo, item.SkillId))))
                .ToList(),
            Total = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = result.TotalCount == 0 ? 0 : (int)Math.Ceiling(result.TotalCount / (double)result.PageSize),
            ScrapedAt = null
        };
    }

    /// <inheritdoc />
    public async Task<List<SkillCatalogSourceDto>> GetSourcesAsync(CancellationToken ct = default)
        => (await _catalogService.GetSourcesAsync(ct))
            .Select(source => new SkillCatalogSourceDto
            {
                SourceKey = source.Key,
                DisplayName = source.DisplayName,
                Source = source.Key,
                Owner = string.Empty,
                Repo = string.Empty,
                SkillCount = 0,
                TotalInstalls = 0
            })
            .ToList();

    /// <inheritdoc />
    public async Task<SkillCatalogItemDto?> GetCatalogItemAsync(string owner, string repo, string skillId, CancellationToken ct = default)
    {
        var normalizedOwner = NormalizeRequired(owner, nameof(owner));
        var normalizedRepo = NormalizeRequired(repo, nameof(repo));
        var normalizedSkillId = NormalizeRequired(skillId, nameof(skillId));

        var item = await _catalogService.GetAsync(normalizedOwner, normalizedRepo, normalizedSkillId, ct);
        if (item is null)
            return null;

        var isInstalled = (await LoadInstalledIdentitySetAsync(ct))
            .Contains(BuildCatalogIdentity(normalizedOwner, normalizedRepo, normalizedSkillId));
        return ToCatalogItemDto(item, isInstalled);
    }

    /// <inheritdoc />
    public async Task<List<InstalledSkillDto>> GetInstalledAsync(GetInstalledSkillsInput input, CancellationToken ct = default)
    {
        IEnumerable<ManagedInstalledSkill> items = await _managedSkillStore.GetInstalledAsync(ct);
        var query = Normalize(input.Query);

        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items.Where(item =>
                item.SkillId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Owner.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Repo.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(item.Source) && item.Source.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        return items
            .OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(ToInstalledSkillDto)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<InstalledSkillDto> InstallAsync(InstallSkillInput input, CancellationToken ct = default)
    {
        var owner = NormalizeRequired(input.Owner, nameof(input.Owner));
        var repo = NormalizeRequired(input.Repo, nameof(input.Repo));
        var skillId = NormalizeRequired(input.SkillId, nameof(input.SkillId));
        var sourceKey = string.IsNullOrWhiteSpace(input.SourceKey) ? SkillSourceKeys.SkillsSh : input.SourceKey.Trim();

        var catalogItem = await _catalogService.GetAsync(owner, repo, skillId, ct)
            ?? throw new KeyNotFoundException($"Skill '{owner}/{repo}:{skillId}' was not found in the catalog.");

        if (!string.Equals(catalogItem.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Skill '{owner}/{repo}:{skillId}' does not belong to source '{sourceKey}'.");

        var installed = await _managedSkillStore.InstallAsync(catalogItem, ct);
        return ToInstalledSkillDto(installed);
    }

    /// <inheritdoc />
    public async Task<bool> UninstallAsync(UninstallSkillInput input, CancellationToken ct = default)
    {
        var owner = NormalizeRequired(input.Owner, nameof(input.Owner));
        var repo = NormalizeRequired(input.Repo, nameof(input.Repo));
        var skillId = NormalizeRequired(input.SkillId, nameof(input.SkillId));

        var installed = (await _managedSkillStore.GetInstalledAsync(ct))
            .FirstOrDefault(item =>
                string.Equals(item.Owner, owner, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Repo, repo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.SkillId, skillId, StringComparison.OrdinalIgnoreCase));

        return installed is not null && await _managedSkillStore.RemoveAsync(installed.Id, ct);
    }

    /// <inheritdoc />
    public async Task<List<AgentRoleSkillBindingDto>> GetAgentRoleBindingsAsync(Guid agentRoleId, CancellationToken ct = default)
    {
        var bindings = await _agentRoleSkillBindings.AsNoTracking()
            .Where(binding => binding.AgentRoleId == agentRoleId)
            .OrderBy(binding => binding.CreatedAt)
            .ToListAsync(ct);

        var installedByKey = (await _managedSkillStore.GetInstalledAsync(ct))
            .ToDictionary(item => item.InstallKey, StringComparer.OrdinalIgnoreCase);

        return bindings
            .Select(binding => ToAgentRoleBindingDto(
                binding,
                installedByKey.TryGetValue(binding.SkillInstallKey, out var installed) ? installed : null))
            .ToList();
    }

    /// <inheritdoc />
    public async Task ReplaceAgentRoleBindingsAsync(ReplaceAgentRoleSkillBindingsRequest request, CancellationToken ct = default)
    {
        var roleExists = await _agentRoles.AsNoTracking()
            .AnyAsync(role => role.Id == request.AgentRoleId && role.IsActive, ct);
        if (!roleExists)
            throw new KeyNotFoundException($"Agent role '{request.AgentRoleId}' was not found.");

        var normalizedBindings = request.Bindings
            .Where(binding => !string.IsNullOrWhiteSpace(binding.SkillInstallKey))
            .GroupBy(binding => binding.SkillInstallKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();

        var existingBindings = await _agentRoleSkillBindings
            .Where(binding => binding.AgentRoleId == request.AgentRoleId)
            .ToListAsync(ct);
        _agentRoleSkillBindings.RemoveRange(existingBindings);

        foreach (var binding in normalizedBindings)
        {
            _agentRoleSkillBindings.Add(new AgentRoleSkillBinding
            {
                AgentRoleId = request.AgentRoleId,
                SkillInstallKey = binding.SkillInstallKey.Trim(),
                SkillId = binding.SkillId.Trim(),
                Name = binding.Name.Trim(),
                DisplayName = binding.DisplayName.Trim(),
                Source = binding.Source.Trim(),
                Owner = binding.Owner.Trim(),
                Repo = binding.Repo.Trim(),
                GithubUrl = string.IsNullOrWhiteSpace(binding.GithubUrl) ? null : binding.GithubUrl.Trim(),
                IsEnabled = binding.IsEnabled
            });
        }

        await _repositoryContext.SaveChangesAsync(ct);
    }

    private async Task<HashSet<string>> LoadInstalledIdentitySetAsync(CancellationToken ct)
        => (await _managedSkillStore.GetInstalledAsync(ct))
            .Select(item => BuildCatalogIdentity(item.Owner, item.Repo, item.SkillId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static SkillCatalogItemDto ToCatalogItemDto(PlatformSkillCatalogEntry item, bool isInstalled)
        => new()
        {
            SourceKey = item.SourceKey,
            Source = $"{item.Owner}/{item.Repo}",
            SkillId = item.SkillId,
            Name = item.Name,
            DisplayName = item.DisplayName,
            Description = item.Description,
            Installs = item.Installs,
            Owner = item.Owner,
            Repo = item.Repo,
            GithubUrl = item.RepositoryUrl,
            IsInstalled = isInstalled
        };

    private static InstalledSkillDto ToInstalledSkillDto(ManagedInstalledSkill item)
        => new()
        {
            Id = item.Id,
            InstallKey = item.InstallKey,
            SourceKey = item.SourceKey,
            Scope = SkillScopes.Global,
            ProjectId = null,
            ProjectName = null,
            Source = string.IsNullOrWhiteSpace(item.Source) ? $"{item.Owner}/{item.Repo}" : item.Source,
            Owner = item.Owner,
            Repo = item.Repo,
            SkillId = item.SkillId,
            Name = item.Name,
            DisplayName = item.DisplayName,
            Description = null,
            GithubUrl = item.GithubUrl,
            Installs = item.Installs,
            InstallMode = SkillInstallModes.Managed,
            TargetAgents = [],
            InstallRootPath = item.InstallRootPath,
            IsEnabled = true,
            Status = item.Status,
            StatusMessage = item.StatusMessage,
            IsManaged = true,
            SourceRevision = item.SourceRevision,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };

    private static string BuildCatalogIdentity(string owner, string repo, string skillId)
        => $"{owner.Trim()}::{repo.Trim()}::{skillId.Trim()}".ToLowerInvariant();

    private static AgentRoleSkillBindingDto ToAgentRoleBindingDto(
        AgentRoleSkillBinding binding,
        ManagedInstalledSkill? installed)
        => new()
        {
            Id = binding.Id,
            AgentRoleId = binding.AgentRoleId,
            SkillInstallKey = binding.SkillInstallKey,
            SkillId = binding.SkillId,
            Name = binding.Name,
            DisplayName = binding.DisplayName,
            Source = binding.Source,
            Owner = binding.Owner,
            Repo = binding.Repo,
            GithubUrl = binding.GithubUrl,
            IsEnabled = binding.IsEnabled,
            ResolutionStatus = installed is null ? SkillBindingResolutionStatuses.Missing : SkillBindingResolutionStatuses.Resolved,
            ResolutionMessage = installed is null
                ? $"未在受管目录中找到 Skill：{binding.SkillInstallKey}"
                : null,
            InstallRootPath = installed?.InstallRootPath,
            CreatedAt = binding.CreatedAt,
            UpdatedAt = binding.UpdatedAt
        };

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRequired(string? value, string fieldName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Field '{fieldName}' is required.")
            : value.Trim();
}



