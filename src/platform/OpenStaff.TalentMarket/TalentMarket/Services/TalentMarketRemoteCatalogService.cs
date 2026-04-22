using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OpenStaff.TalentMarket.Services;

public interface ITalentMarketRemoteCatalogService
{
    string SourceKey { get; }

    string DisplayName { get; }

    Task<IReadOnlyList<RemoteTalentMarketTemplateSummary>> GetTemplatesAsync(CancellationToken ct = default);

    Task<RemoteTalentMarketTemplate?> GetTemplateAsync(string templateId, CancellationToken ct = default);
}

public sealed record RemoteTalentMarketTemplateSummary(
    string TemplateId,
    string File,
    string Name,
    string? Job,
    string? JobTitle,
    string? Description,
    string? Avatar,
    string? ModelName,
    string? Source,
    bool IsBuiltin,
    bool IsActive,
    int McpCount,
    int SkillCount);

public sealed record RemoteTalentMarketTemplate(
    RemoteTalentMarketTemplateSummary Summary,
    string Content);

public sealed class TalentMarketRemoteCatalogService : ITalentMarketRemoteCatalogService
{
    public const string HttpClientName = "talent-market";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TalentMarketRemoteCatalogService> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    private TalentMarketIndexCache? _cache;

    public TalentMarketRemoteCatalogService(
        IHttpClientFactory httpClientFactory,
        ILogger<TalentMarketRemoteCatalogService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string SourceKey => "sunny-dotnet-agents";

    public string DisplayName => "Sunny-DotNet/agents";

    public async Task<IReadOnlyList<RemoteTalentMarketTemplateSummary>> GetTemplatesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (_cache is { } cache && cache.ExpiresAt > now)
            return cache.Items;

        await _cacheLock.WaitAsync(ct);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (_cache is { } refreshedCache && refreshedCache.ExpiresAt > now)
                return refreshedCache.Items;

            var items = await FetchTemplatesAsync(ct);
            _cache = new TalentMarketIndexCache(items, now.AddMinutes(1));
            return items;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<RemoteTalentMarketTemplate?> GetTemplateAsync(string templateId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return null;

        var summary = (await GetTemplatesAsync(ct))
            .FirstOrDefault(item => string.Equals(item.TemplateId, templateId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (summary is null)
            return null;

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var content = await client.GetStringAsync($"templates/{Uri.EscapeDataString(summary.File)}", ct);
        return new RemoteTalentMarketTemplate(summary, content);
    }

    private async Task<IReadOnlyList<RemoteTalentMarketTemplateSummary>> FetchTemplatesAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var document = await client.GetFromJsonAsync<RemoteTalentMarketIndexDocument>("index.json", JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to load the remote talent-market index.");

        var templates = document.Templates?
            .Where(item => !string.IsNullOrWhiteSpace(item.Id)
                && !string.IsNullOrWhiteSpace(item.File)
                && !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new RemoteTalentMarketTemplateSummary(
                item.Id!.Trim(),
                item.File!.Trim(),
                item.Name!.Trim(),
                NormalizeOptional(item.Job),
                NormalizeOptional(item.JobTitle),
                NormalizeOptional(item.Description),
                NormalizeOptional(item.Avatar),
                NormalizeOptional(item.Model),
                NormalizeOptional(item.Source),
                item.IsBuiltin,
                item.IsActive,
                item.McpCount,
                item.SkillCount))
            .ToList()
            ?? [];

        _logger.LogInformation("Loaded {Count} remote role templates from {SourceKey}.", templates.Count, SourceKey);
        return templates;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record TalentMarketIndexCache(
        IReadOnlyList<RemoteTalentMarketTemplateSummary> Items,
        DateTimeOffset ExpiresAt);

    private sealed class RemoteTalentMarketIndexDocument
    {
        public List<RemoteTalentMarketIndexItem>? Templates { get; set; }
    }

    private sealed class RemoteTalentMarketIndexItem
    {
        public string? File { get; set; }

        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Job { get; set; }

        public string? JobTitle { get; set; }

        public string? Description { get; set; }

        public string? Avatar { get; set; }

        public string? Model { get; set; }

        public string? Source { get; set; }

        public bool IsBuiltin { get; set; }

        public bool IsActive { get; set; }

        public int McpCount { get; set; }

        public int SkillCount { get; set; }
    }
}
