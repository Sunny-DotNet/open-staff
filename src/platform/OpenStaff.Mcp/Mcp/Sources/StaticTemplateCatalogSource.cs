using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenStaff.Mcp.Models;

namespace OpenStaff.Mcp.Sources;

/// <summary>
/// zh-CN: 基于静态模板仓库的 MCP 目录源，当前读取 Pages 发布的 index.json 和 templates/*.mcp.json。
/// en: MCP catalog source backed by a static template repository that reads the published Pages index.json and templates/*.mcp.json documents.
/// </summary>
public sealed class StaticTemplateCatalogSource : IMcpCatalogSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenStaffMcpOptions _options;
    private readonly ILogger<StaticTemplateCatalogSource> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    private TemplateCatalogCache? _cache;

    public StaticTemplateCatalogSource(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenStaffMcpOptions> options,
        ILogger<StaticTemplateCatalogSource> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string SourceKey => "mcps";

    public string DisplayName => "MCP Templates";

    public int Priority => 0;

    public async Task<IReadOnlyList<CatalogEntry>> SearchAsync(CatalogSearchQuery query, CancellationToken cancellationToken = default)
        => (await GetEntriesAsync(cancellationToken))
            .Select(item => item.Entry)
            .ToList();

    public async Task<CatalogEntry?> GetByIdAsync(string entryId, CancellationToken cancellationToken = default)
        => (await GetEntriesAsync(cancellationToken))
            .FirstOrDefault(item => string.Equals(item.Entry.EntryId, entryId, StringComparison.OrdinalIgnoreCase))
            ?.Entry;

    private async Task<IReadOnlyList<TemplateCatalogItem>> GetEntriesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (_cache is { } cache && cache.ExpiresAt > now)
            return cache.Items;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (_cache is { } refreshedCache && refreshedCache.ExpiresAt > now)
                return refreshedCache.Items;

            var items = await FetchEntriesAsync(cancellationToken);
            _cache = new TemplateCatalogCache(
                items,
                now.AddSeconds(Math.Max(30, _options.TemplateCatalogCacheSeconds)));
            return items;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<IReadOnlyList<TemplateCatalogItem>> FetchEntriesAsync(CancellationToken cancellationToken)
    {
        var indexJson = await GetStringAsync(BuildUri("index.json"), cancellationToken);
        var summaries = ParseIndex(indexJson);
        var fetchTasks = summaries.Select(summary => ResolveTemplateAsync(summary, cancellationToken)).ToList();
        var resolved = await Task.WhenAll(fetchTasks);

        return resolved
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();
    }

    private async Task<TemplateCatalogItem?> ResolveTemplateAsync(TemplateIndexSummary summary, CancellationToken cancellationToken)
    {
        foreach (var slug in GetCandidateSlugs(summary))
        {
            var templateUri = BuildUri($"templates/{slug}.mcp.json");
            var rawTemplate = await TryGetStringAsync(templateUri, cancellationToken);
            if (rawTemplate is null)
                continue;

            var template = ParseTemplate(rawTemplate, slug);
            if (template is null)
                continue;

            return new TemplateCatalogItem(BuildCatalogEntry(summary, template, rawTemplate));
        }

        _logger.LogWarning("Unable to resolve template document for static MCP summary {Key} ({Homepage})", summary.Key, summary.Homepage);
        return null;
    }

    private CatalogEntry BuildCatalogEntry(TemplateIndexSummary summary, TemplateDocument template, string rawTemplate)
    {
        var transportTypes = template.Profiles
            .Select(profile => TryMapTransportType(profile.TransportType))
            .Where(type => type.HasValue)
            .Select(type => type!.Value)
            .Distinct()
            .ToList();

        var installChannels = template.Profiles
            .Select(profile => TryBuildInstallChannel(template, profile, rawTemplate))
            .Where(channel => channel is not null)
            .Select(channel => channel!)
            .ToList();

        return new CatalogEntry
        {
            EntryId = template.TemplateId,
            SourceKey = SourceKey,
            Name = template.Key,
            DisplayName = string.IsNullOrWhiteSpace(template.DisplayName) ? summary.DisplayName : template.DisplayName,
            Description = template.Description ?? summary.Description,
            Category = template.Category ?? summary.Category,
            Homepage = template.Homepage ?? summary.Homepage,
            RepositoryUrl = template.Homepage ?? summary.Homepage,
            TransportTypes = transportTypes,
            InstallChannels = installChannels
        };
    }

    private InstallChannel? TryBuildInstallChannel(TemplateDocument template, TemplateProfile profile, string rawTemplate)
    {
        var transportType = TryMapTransportType(profile.TransportType);
        if (!transportType.HasValue)
            return null;

        if (string.Equals(profile.ProfileType, "package", StringComparison.OrdinalIgnoreCase))
        {
            var channelType = TryMapPackageChannelType(profile);
            if (!channelType.HasValue || string.IsNullOrWhiteSpace(profile.PackageName))
                return null;

            return new InstallChannel
            {
                ChannelId = profile.Id,
                ChannelType = channelType.Value,
                TransportType = transportType.Value,
                Version = profile.PackageVersion,
                PackageIdentifier = profile.PackageName,
                Metadata = BuildMetadata(rawTemplate, template.TemplateId, profile.Id)
            };
        }

        if (string.Equals(profile.ProfileType, "remote", StringComparison.OrdinalIgnoreCase)
            && TryBuildRemoteMetadata(template, profile, out var endpointUrl, out var metadata))
        {
            var mergedMetadata = BuildMetadata(rawTemplate, template.TemplateId, profile.Id);
            foreach (var pair in metadata)
                mergedMetadata[pair.Key] = pair.Value;

            return new InstallChannel
            {
                ChannelId = profile.Id,
                ChannelType = McpChannelType.Remote,
                TransportType = transportType.Value,
                ArtifactUrl = endpointUrl,
                Metadata = mergedMetadata
            };
        }

        return null;
    }

    private static Dictionary<string, string> BuildMetadata(string rawTemplate, string templateId, string profileId)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [McpSourceMetadataKeys.RawTemplateJson] = rawTemplate,
            [McpSourceMetadataKeys.TemplateId] = templateId,
            [McpSourceMetadataKeys.TemplateProfileId] = profileId
        };
    }

    private static bool TryBuildRemoteMetadata(
        TemplateDocument template,
        TemplateProfile profile,
        out string endpointUrl,
        out Dictionary<string, string> metadata)
    {
        endpointUrl = string.Empty;
        metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var defaults = BuildParameterDefaultLookup(template);
        if (!TryResolveTemplateString(profile.UrlTemplate, defaults, out endpointUrl)
            || !Uri.TryCreate(endpointUrl, UriKind.Absolute, out _))
        {
            return false;
        }

        var headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in profile.HeadersTemplate)
        {
            if (!TryResolveTemplateString(pair.Value, defaults, out var resolved))
                return false;

            if (!string.IsNullOrWhiteSpace(resolved))
                headers[pair.Key] = resolved;
        }

        metadata[PackageManagers.InstallChannelMetadataKeys.EndpointUrl] = endpointUrl;
        if (headers.Count > 0)
            metadata[PackageManagers.InstallChannelMetadataKeys.EndpointHeaders] = JsonSerializer.Serialize(headers);

        return true;
    }

    private static Dictionary<string, string> BuildParameterDefaultLookup(TemplateDocument template)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in template.ParameterSchema)
        {
            if (parameter.DefaultValue is null)
                continue;

            if (parameter.DefaultValue is bool boolValue)
                lookup[parameter.Key] = boolValue ? "true" : "false";
            else
                lookup[parameter.Key] = Convert.ToString(parameter.DefaultValue, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return lookup;
    }

    private static bool TryResolveTemplateString(string? value, IReadOnlyDictionary<string, string> defaults, out string resolved)
    {
        resolved = value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var current = value;
        while (true)
        {
            var start = current.IndexOf("${param:", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                break;

            var end = current.IndexOf('}', start);
            if (end < 0)
                return false;

            var keyStart = start + "${param:".Length;
            var key = current[keyStart..end];
            if (!defaults.TryGetValue(key, out var defaultValue))
                return false;

            current = $"{current[..start]}{defaultValue}{current[(end + 1)..]}";
        }

        resolved = current.Trim();
        return !string.IsNullOrWhiteSpace(resolved);
    }

    private async Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(OpenStaffMcpModule));
        using var response = await client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private async Task<string?> TryGetStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(OpenStaffMcpModule));
        using var response = await client.GetAsync(uri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private Uri BuildUri(string relativePath)
    {
        var baseUri = _options.TemplateCatalogBaseUrl.TrimEnd('/');
        return new Uri($"{baseUri}/{relativePath}", UriKind.Absolute);
    }

    private static IReadOnlyList<TemplateIndexSummary> ParseIndex(string json)
    {
        if (JsonNode.Parse(json) is not JsonArray array)
            return [];

        return array
            .OfType<JsonObject>()
            .Select(item => new TemplateIndexSummary(
                Key: ReadString(item, "key") ?? string.Empty,
                DisplayName: ReadString(item, "display_name") ?? string.Empty,
                Description: ReadString(item, "description"),
                Category: ReadString(item, "category"),
                Homepage: ReadString(item, "homepage")))
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToList();
    }

    private static TemplateDocument? ParseTemplate(string rawTemplate, string fallbackSlug)
    {
        if (JsonNode.Parse(rawTemplate) is not JsonObject node)
            return null;

        var templateId = ReadString(node, "template_id");
        var key = ReadString(node, "key");
        if (string.IsNullOrWhiteSpace(templateId) || string.IsNullOrWhiteSpace(key))
            return null;

        return new TemplateDocument(
            TemplateId: templateId,
            Key: key,
            DisplayName: ReadString(node, "display_name") ?? key,
            Description: ReadString(node, "description"),
            Category: ReadString(node, "category"),
            Homepage: ReadString(node, "homepage"),
            Slug: fallbackSlug,
            Profiles: ReadProfiles(node),
            ParameterSchema: ReadParameterSchema(node));
    }

    private static List<TemplateProfile> ReadProfiles(JsonObject node)
    {
        if (node["profiles"] is not JsonArray profilesArray)
            return [];

        return profilesArray
            .OfType<JsonObject>()
            .Select(profile => new TemplateProfile(
                Id: ReadString(profile, "id") ?? string.Empty,
                ProfileType: ReadString(profile, "profile_type") ?? string.Empty,
                TransportType: ReadString(profile, "transport_type") ?? "stdio",
                RunnerKind: ReadString(profile, "runner_kind"),
                Runner: ReadString(profile, "runner"),
                Ecosystem: ReadString(profile, "ecosystem"),
                PackageName: ReadString(profile, "package_name"),
                PackageVersion: ReadString(profile, "package_version"),
                UrlTemplate: ReadString(profile, "url_template"),
                HeadersTemplate: ReadDictionary(profile, "headers_template")))
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Id))
            .ToList();
    }

    private static List<TemplateParameter> ReadParameterSchema(JsonObject node)
    {
        if (node["parameter_schema"] is not JsonArray parametersArray)
            return [];

        return parametersArray
            .OfType<JsonObject>()
            .Select(parameter => new TemplateParameter(
                Key: ReadString(parameter, "key") ?? string.Empty,
                Type: ReadString(parameter, "type") ?? "string",
                Required: ReadBoolean(parameter, "required"),
                DefaultValue: ReadValue(parameter["default_value"]),
                AppliesToProfiles: ReadStringList(parameter, "applies_to_profiles")))
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Key))
            .ToList();
    }

    private static McpChannelType? TryMapPackageChannelType(TemplateProfile profile)
    {
        var ecosystem = profile.Ecosystem?.Trim().ToLowerInvariant();
        var runner = profile.Runner?.Trim().ToLowerInvariant();

        return ecosystem switch
        {
            "npm" => McpChannelType.Npm,
            "python" or "pypi" => McpChannelType.Pypi,
            _ => runner switch
            {
                "npx" => McpChannelType.Npm,
                "uvx" => McpChannelType.Pypi,
                _ => null
            }
        };
    }

    private static McpTransportType? TryMapTransportType(string? transportType)
    {
        return transportType?.Trim().ToLowerInvariant() switch
        {
            "stdio" => McpTransportType.Stdio,
            "http" => McpTransportType.Http,
            "sse" => McpTransportType.Sse,
            "streamable-http" => McpTransportType.StreamableHttp,
            _ => null
        };
    }

    private static IEnumerable<string> GetCandidateSlugs(TemplateIndexSummary summary)
    {
        var candidates = new List<string>();
        if (string.Equals(summary.Key, "github", StringComparison.OrdinalIgnoreCase))
        {
            if (summary.Homepage?.Contains("github/github-mcp-server", StringComparison.OrdinalIgnoreCase) == true
                || summary.Description?.Contains("Official GitHub MCP", StringComparison.OrdinalIgnoreCase) == true)
            {
                candidates.Add("github-official");
                candidates.Add("github-legacy");
            }
            else
            {
                candidates.Add("github-legacy");
                candidates.Add("github-official");
            }
        }

        candidates.Add(summary.Key);
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonObject node, string propertyName)
        => node[propertyName] is JsonValue value && value.TryGetValue<string>(out var result)
            ? result
            : null;

    private static bool ReadBoolean(JsonObject node, string propertyName)
        => node[propertyName] is JsonValue value && value.TryGetValue<bool>(out var result) && result;

    private static Dictionary<string, string?> ReadDictionary(JsonObject node, string propertyName)
    {
        if (node[propertyName] is not JsonObject objectNode)
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        return objectNode.ToDictionary(
            pair => pair.Key,
            pair => pair.Value is JsonValue value && value.TryGetValue<string>(out var stringValue) ? stringValue : pair.Value?.ToJsonString(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> ReadStringList(JsonObject node, string propertyName)
    {
        if (node[propertyName] is not JsonArray array)
            return [];

        return array
            .Select(item => item is JsonValue value && value.TryGetValue<string>(out var stringValue) ? stringValue : null)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToList();
    }

    private static object? ReadValue(JsonNode? node)
    {
        if (node is null)
            return null;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var stringValue))
                return stringValue;
            if (value.TryGetValue<bool>(out var boolValue))
                return boolValue;
            if (value.TryGetValue<int>(out var intValue))
                return intValue;
            if (value.TryGetValue<long>(out var longValue))
                return longValue;
            if (value.TryGetValue<double>(out var doubleValue))
                return doubleValue;
        }

        return node.ToJsonString();
    }

    private sealed record TemplateCatalogCache(IReadOnlyList<TemplateCatalogItem> Items, DateTimeOffset ExpiresAt);

    private sealed record TemplateCatalogItem(CatalogEntry Entry);

    private sealed record TemplateIndexSummary(
        string Key,
        string DisplayName,
        string? Description,
        string? Category,
        string? Homepage);

    private sealed record TemplateDocument(
        string TemplateId,
        string Key,
        string DisplayName,
        string? Description,
        string? Category,
        string? Homepage,
        string Slug,
        IReadOnlyList<TemplateProfile> Profiles,
        IReadOnlyList<TemplateParameter> ParameterSchema);

    private sealed record TemplateProfile(
        string Id,
        string ProfileType,
        string TransportType,
        string? RunnerKind,
        string? Runner,
        string? Ecosystem,
        string? PackageName,
        string? PackageVersion,
        string? UrlTemplate,
        IReadOnlyDictionary<string, string?> HeadersTemplate);

    private sealed record TemplateParameter(
        string Key,
        string Type,
        bool Required,
        object? DefaultValue,
        IReadOnlyList<string> AppliesToProfiles);
}
