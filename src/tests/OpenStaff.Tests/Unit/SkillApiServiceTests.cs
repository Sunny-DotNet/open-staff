using Moq;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;
using OpenStaff.Application.Skills.Services;
using OpenStaff.Entities;
using OpenStaff.Repositories;
using OpenStaff.Skills.Services;
using PlatformSkillCatalogEntry = OpenStaff.Skills.Models.SkillCatalogEntry;
using PlatformSkillCatalogQuery = OpenStaff.Skills.Models.SkillCatalogQuery;
using PlatformSkillCatalogSearchResult = OpenStaff.Skills.Models.SkillCatalogSearchResult;
using PlatformSkillCatalogSource = OpenStaff.Skills.Models.SkillCatalogSource;

namespace OpenStaff.Tests.Unit;

public sealed class SkillApiServiceTests
{
    [Fact]
    public async Task SearchCatalogAsync_ShouldMarkManagedSkillsAsInstalled()
    {
        var service = CreateService(
            new FakeSkillCatalogService(
                searchResult: new PlatformSkillCatalogSearchResult
                {
                    Items =
                    [
                        CreateCatalogItem("vercel-labs", "skills", "find-skills", "Find Skills", installs: 10),
                        CreateCatalogItem("anthropics", "skills", "frontend-design", "Frontend Design", installs: 5)
                    ],
                    TotalCount = 2,
                    Page = 1,
                    PageSize = 24
                }),
            new FakeManagedSkillStore(
            [
                CreateInstalledSkill("vercel-labs", "skills", "find-skills", "Find Skills")
            ]));

        var result = await service.SearchCatalogAsync(new SkillCatalogQueryInput
        {
            Query = "find",
            SortBy = "installs",
            SortOrder = "desc"
        });

        var item = Assert.Single(result.Items);
        Assert.Equal("find-skills", item.SkillId);
        Assert.True(item.IsInstalled);
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task GetInstalledAsync_ShouldReturnManagedSkills()
    {
        var installedAt = DateTime.UtcNow.AddMinutes(-10);
        var updatedAt = DateTime.UtcNow;
        var service = CreateService(
            new FakeSkillCatalogService(),
            new FakeManagedSkillStore(
            [
                CreateInstalledSkill("microsoft", "skills", "maps", "Maps", installedAt, updatedAt),
                CreateInstalledSkill("vercel-labs", "skills", "find-skills", "Find Skills", updatedAt.AddMinutes(-2), updatedAt.AddMinutes(-1))
            ]));

        var result = await service.GetInstalledAsync(new GetInstalledSkillsInput
        {
            Query = "map"
        });

        var item = Assert.Single(result);
        Assert.Equal("maps", item.SkillId);
        Assert.True(item.IsManaged);
        Assert.Equal(SkillInstallStatuses.Installed, item.Status);
        Assert.Equal(SkillInstallModes.Managed, item.InstallMode);
        Assert.Equal(installedAt, item.CreatedAt);
        Assert.Equal(updatedAt, item.UpdatedAt);
    }

    [Fact]
    public async Task InstallAndUninstallAsync_ShouldDelegateToPlatformServices()
    {
        var catalogItem = CreateCatalogItem("microsoft", "skills", "maps", "Maps", installs: 42);
        var managedSkillStore = new FakeManagedSkillStore();
        var service = CreateService(
            new FakeSkillCatalogService(
                getEntries: new Dictionary<string, PlatformSkillCatalogEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    [BuildKey("microsoft", "skills", "maps")] = catalogItem
                }),
            managedSkillStore);

        var installed = await service.InstallAsync(new InstallSkillInput
        {
            SourceKey = SkillSourceKeys.SkillsSh,
            Owner = "microsoft",
            Repo = "skills",
            SkillId = "maps"
        });

        Assert.Equal("maps", installed.SkillId);
        Assert.Equal("microsoft", installed.Owner);
        Assert.Equal("skills", installed.Repo);
        Assert.Equal(1, managedSkillStore.InstallCalls.Count);

        var removed = await service.UninstallAsync(new UninstallSkillInput
        {
            Owner = "microsoft",
            Repo = "skills",
            SkillId = "maps"
        });

        Assert.True(removed);
        Assert.Equal(installed.Id, managedSkillStore.LastRemovedId);
    }

    private static SkillApiService CreateService(
        ISkillCatalogService catalogService,
        IManagedSkillStore managedSkillStore)
        => new(
            catalogService,
            managedSkillStore,
            new Mock<IAgentRoleRepository>().Object,
            new Mock<IAgentRoleSkillBindingRepository>().Object,
            new Mock<IRepositoryContext>().Object);

    private static PlatformSkillCatalogEntry CreateCatalogItem(
        string owner,
        string repo,
        string skillId,
        string displayName,
        int installs)
        => new()
        {
            SourceKey = SkillSourceKeys.SkillsSh,
            Owner = owner,
            Repo = repo,
            SkillId = skillId,
            Name = skillId,
            DisplayName = displayName,
            Description = $"{displayName} description",
            RepositoryUrl = $"https://github.com/{owner}/{repo}",
            Installs = installs
        };

    private static ManagedInstalledSkill CreateInstalledSkill(
        string owner,
        string repo,
        string skillId,
        string displayName,
        DateTime? installedAt = null,
        DateTime? updatedAt = null)
        => new(
            Id: Guid.NewGuid(),
            InstallKey: $"{owner}--{repo}--{skillId}",
            SourceKey: SkillSourceKeys.SkillsSh,
            Source: $"{owner}/{repo}",
            Owner: owner,
            Repo: repo,
            SkillId: skillId,
            Name: skillId,
            DisplayName: displayName,
            GithubUrl: $"https://github.com/{owner}/{repo}",
            Installs: 42,
            InstallRootPath: Path.Combine(Path.GetTempPath(), "openstaff-skill-tests", owner, repo, skillId),
            Status: SkillInstallStatuses.Installed,
            StatusMessage: null,
            SourceRevision: "sha-1",
            CreatedAt: installedAt ?? DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt: updatedAt ?? DateTime.UtcNow);

    private static string BuildKey(string owner, string repo, string skillId)
        => $"{owner}/{repo}:{skillId}";

    private sealed class FakeSkillCatalogService : ISkillCatalogService
    {
        private readonly PlatformSkillCatalogSearchResult _searchResult;
        private readonly IReadOnlyDictionary<string, PlatformSkillCatalogEntry> _getEntries;
        private readonly IReadOnlyList<PlatformSkillCatalogSource> _sources;

        public FakeSkillCatalogService(
            PlatformSkillCatalogSearchResult? searchResult = null,
            IReadOnlyDictionary<string, PlatformSkillCatalogEntry>? getEntries = null,
            IReadOnlyList<PlatformSkillCatalogSource>? sources = null)
        {
            _searchResult = searchResult ?? new PlatformSkillCatalogSearchResult();
            _getEntries = getEntries ?? new Dictionary<string, PlatformSkillCatalogEntry>(StringComparer.OrdinalIgnoreCase);
            _sources = sources ?? [new PlatformSkillCatalogSource { Key = SkillSourceKeys.SkillsSh, DisplayName = "skills.sh" }];
        }

        public Task<PlatformSkillCatalogSearchResult> SearchAsync(PlatformSkillCatalogQuery query, CancellationToken cancellationToken = default)
        {
            IEnumerable<PlatformSkillCatalogEntry> items = _searchResult.Items;

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                items = items.Where(item =>
                    item.SkillId.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase)
                    || item.Name.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase)
                    || item.DisplayName.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult(new PlatformSkillCatalogSearchResult
            {
                Items = items.ToList(),
                TotalCount = _searchResult.TotalCount,
                Page = _searchResult.Page == 0 ? 1 : _searchResult.Page,
                PageSize = _searchResult.PageSize == 0 ? 24 : _searchResult.PageSize
            });
        }

        public Task<PlatformSkillCatalogEntry?> GetAsync(string owner, string repo, string skillId, CancellationToken cancellationToken = default)
            => Task.FromResult(_getEntries.TryGetValue(BuildKey(owner, repo, skillId), out var item) ? item : null);

        public Task<IReadOnlyList<PlatformSkillCatalogSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_sources);
    }

    private sealed class FakeManagedSkillStore : IManagedSkillStore
    {
        private readonly List<ManagedInstalledSkill> _installed;

        public FakeManagedSkillStore(IEnumerable<ManagedInstalledSkill>? installed = null)
        {
            _installed = installed?.ToList() ?? [];
        }

        public List<PlatformSkillCatalogEntry> InstallCalls { get; } = [];

        public Guid? LastRemovedId { get; private set; }

        public Task<IReadOnlyList<ManagedInstalledSkill>> GetInstalledAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ManagedInstalledSkill>>(_installed.ToList());

        public Task<ManagedInstalledSkill?> GetByInstallKeyAsync(string installKey, CancellationToken ct = default)
            => Task.FromResult(_installed.FirstOrDefault(item =>
                string.Equals(item.InstallKey, installKey, StringComparison.OrdinalIgnoreCase)));

        public Task<ManagedInstalledSkill?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_installed.FirstOrDefault(item => item.Id == id));

        public Task<ManagedInstalledSkill> InstallAsync(PlatformSkillCatalogEntry catalogEntry, CancellationToken ct = default)
        {
            InstallCalls.Add(catalogEntry);

            var installed = CreateInstalledSkill(
                catalogEntry.Owner,
                catalogEntry.Repo,
                catalogEntry.SkillId,
                catalogEntry.DisplayName);

            _installed.RemoveAll(item =>
                string.Equals(item.Owner, installed.Owner, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Repo, installed.Repo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.SkillId, installed.SkillId, StringComparison.OrdinalIgnoreCase));
            _installed.Add(installed);

            return Task.FromResult(installed);
        }

        public Task<bool> RemoveAsync(Guid id, CancellationToken ct = default)
        {
            LastRemovedId = id;
            var removed = _installed.RemoveAll(item => item.Id == id) > 0;
            return Task.FromResult(removed);
        }

        public Task<SkillStoreMaintenanceResult> CheckForUpdatesAsync(CancellationToken ct = default)
            => Task.FromResult(new SkillStoreMaintenanceResult(true, _installed.Count, 0, 0, "ok"));

        public Task<SkillStoreMaintenanceResult> UpdateAsync(CancellationToken ct = default)
            => Task.FromResult(new SkillStoreMaintenanceResult(true, _installed.Count, 0, 0, "ok"));
    }
}

