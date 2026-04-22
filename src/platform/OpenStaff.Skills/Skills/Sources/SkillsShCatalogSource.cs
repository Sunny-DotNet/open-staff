using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenStaff.Skills.Models;
using OpenStaff.Skills.Services;

namespace OpenStaff.Skills.Sources;

/// <summary>
/// skills.sh-backed catalog source.
/// </summary>
public sealed class SkillsShCatalogSource : ISkillCatalogSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGitHubSkillArchiveClient _archiveClient;
    private readonly ILogger<SkillsShCatalogSource> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private IReadOnlyList<SkillCatalogEntry>? _cachedEntries;
    private DateTimeOffset _cacheExpiresAtUtc;

    public SkillsShCatalogSource(
        IHttpClientFactory httpClientFactory,
        IGitHubSkillArchiveClient archiveClient,
        ILogger<SkillsShCatalogSource> logger)
    {
        _httpClientFactory = httpClientFactory;
        _archiveClient = archiveClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public string SourceKey => "skills.sh";

    /// <inheritdoc />
    public string DisplayName => "skills.sh";

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillCatalogEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot
            .Select(CloneEntry)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SkillCatalogEntry?> GetAsync(
        string owner,
        string repo,
        string skillId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        var entry = snapshot.FirstOrDefault(item =>
            string.Equals(item.Owner, owner, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Repo, repo, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.SkillId, skillId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            return null;

        try
        {
            using var archive = await _archiveClient.DownloadAsync(entry, cancellationToken);
            return new SkillCatalogEntry
            {
                SourceKey = entry.SourceKey,
                Owner = entry.Owner,
                Repo = entry.Repo,
                SkillId = entry.SkillId,
                Name = string.IsNullOrWhiteSpace(archive.Document.Name) ? entry.Name : archive.Document.Name,
                DisplayName = string.IsNullOrWhiteSpace(archive.Document.DisplayName) ? entry.DisplayName : archive.Document.DisplayName,
                Description = string.IsNullOrWhiteSpace(archive.Document.Description) ? entry.Description : archive.Document.Description,
                RepositoryUrl = entry.RepositoryUrl,
                Installs = entry.Installs
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to enrich skills.sh catalog entry {Owner}/{Repo}:{SkillId}; returning snapshot metadata only.",
                entry.Owner,
                entry.Repo,
                entry.SkillId);
            return CloneEntry(entry);
        }
    }

    private async Task<IReadOnlyList<SkillCatalogEntry>> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        if (_cachedEntries is not null && DateTimeOffset.UtcNow < _cacheExpiresAtUtc)
            return _cachedEntries;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedEntries is not null && DateTimeOffset.UtcNow < _cacheExpiresAtUtc)
                return _cachedEntries;

            var client = _httpClientFactory.CreateClient(OpenStaffSkillsDefaults.CatalogHttpClientName);
            using var response = await client.GetAsync(
                "https://raw.githubusercontent.com/mastra-ai/skills-api/main/src/registry/scraped-skills.json",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<SkillsShSnapshotPayload>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Failed to deserialize the skills.sh snapshot.");

            _cachedEntries = payload.Skills
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.Owner) &&
                    !string.IsNullOrWhiteSpace(item.Repo) &&
                    !string.IsNullOrWhiteSpace(item.SkillId))
                .Select(item => new SkillCatalogEntry
                {
                    SourceKey = SourceKey,
                    Owner = item.Owner.Trim(),
                    Repo = item.Repo.Trim(),
                    SkillId = item.SkillId.Trim(),
                    Name = string.IsNullOrWhiteSpace(item.Name) ? item.SkillId.Trim() : item.Name.Trim(),
                    DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? (string.IsNullOrWhiteSpace(item.Name) ? item.SkillId.Trim() : item.Name.Trim()) : item.DisplayName.Trim(),
                    Description = null,
                    RepositoryUrl = string.IsNullOrWhiteSpace(item.GithubUrl) ? $"https://github.com/{item.Owner.Trim()}/{item.Repo.Trim()}" : item.GithubUrl.Trim(),
                    Installs = item.Installs
                })
                .ToList();
            _cacheExpiresAtUtc = DateTimeOffset.UtcNow.Add(CacheDuration);

            return _cachedEntries;
        }
        catch (Exception ex) when (_cachedEntries is not null)
        {
            _logger.LogWarning(ex, "Falling back to a stale skills.sh snapshot.");
            return _cachedEntries;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static SkillCatalogEntry CloneEntry(SkillCatalogEntry entry)
        => new()
        {
            SourceKey = entry.SourceKey,
            Owner = entry.Owner,
            Repo = entry.Repo,
            SkillId = entry.SkillId,
            Name = entry.Name,
            DisplayName = entry.DisplayName,
            Description = entry.Description,
            RepositoryUrl = entry.RepositoryUrl,
            Installs = entry.Installs
        };

    private sealed class SkillsShSnapshotPayload
    {
        public List<SkillsShSnapshotItem> Skills { get; set; } = [];
    }

    private sealed class SkillsShSnapshotItem
    {
        public string Owner { get; set; } = string.Empty;
        public string Repo { get; set; } = string.Empty;
        public string SkillId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? GithubUrl { get; set; }
        public int Installs { get; set; }
    }
}
