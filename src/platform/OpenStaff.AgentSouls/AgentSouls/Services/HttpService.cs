using System.Collections.ObjectModel;
using System.Net.Http.Json;
using OpenStaff;
using OpenStaff.AgentSouls.Dtos;

namespace OpenStaff.AgentSouls.Services;

public interface IAgentSoulHttpService
{
    string DefaultAliasName { get; }

    Task<IReadOnlyCollection<AgentSoulValue>> GetAllAsync();
    Task<IReadOnlyDictionary<string, string>> GetAllByLocaleAsync(string? locale = null);
    Task<string> GetAsync(string key, string? locale = null);
}

internal abstract class HttpServiceBase : ServiceBase, IAgentSoulHttpService
{
    protected Lazy<Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>> LazyDictionaryTask { get; }
    protected Lazy<Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>> LazyLocalesTask { get; }
    protected Lazy<Task<IReadOnlyCollection<AgentSoulValue>>> LazyCollectionTask { get; }
    protected abstract string Filename { get; }
    public virtual string DefaultAliasName { get; } = "en";

    protected HttpServiceBase(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        LazyCollectionTask = new Lazy<Task<IReadOnlyCollection<AgentSoulValue>>>(InitializeCollectionAsync);
        LazyDictionaryTask = new Lazy<Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>>(InitializeDictionaryAsync);
        LazyLocalesTask = new Lazy<Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>>(InitializeLocalesAsync);
    }

    private async Task<IReadOnlyCollection<AgentSoulValue>> InitializeCollectionAsync()
    {
        var httpClient = CreateHttpClient();
        var agentSouls = await httpClient.GetFromJsonAsync<List<AgentSoulValue>>(Filename)
            ?? throw new InvalidOperationException($"Failed to load agent soul catalog '{Filename}'.");
        return agentSouls.AsReadOnly();
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> InitializeDictionaryAsync()
    {
        var collection = await LazyCollectionTask.Value;
        var map = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in collection)
        {
            var aliases = new Dictionary<string, string>(item.Aliases, StringComparer.OrdinalIgnoreCase);
            map[item.Key] = new ReadOnlyDictionary<string, string>(aliases);
        }

        return map.AsReadOnly();
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> InitializeLocalesAsync()
    {
        var collection = await LazyCollectionTask.Value;
        var map = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in collection)
        {
            foreach (var locale in item.Aliases)
            {
                if (!map.TryGetValue(locale.Key, out var localeMap))
                {
                    map[locale.Key] = localeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                localeMap[item.Key] = locale.Value;
            }
        }

        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in map)
        {
            result[kvp.Key] = kvp.Value.AsReadOnly();
        }

        return result.AsReadOnly();
    }

    protected virtual HttpClient CreateHttpClient()
    {
        var httpClientFactory = GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(nameof(AgentSouls));
        return httpClient;
    }

    public Task<IReadOnlyCollection<AgentSoulValue>> GetAllAsync() => LazyCollectionTask.Value;

    public async Task<string> GetAsync(string key, string? locale = null)
    {
        var dictionary = await LazyDictionaryTask.Value;
        if (!dictionary.TryGetValue(key, out var aliases))
        {
            throw new KeyNotFoundException($"The key '{key}' was not found.");
        }

        foreach (var candidate in EnumerateLocaleCandidates(locale))
        {
            if (aliases.TryGetValue(candidate, out var localizedValue))
            {
                return localizedValue;
            }
        }

        if (aliases.TryGetValue(DefaultAliasName, out var defaultValue))
        {
            return defaultValue;
        }

        throw new KeyNotFoundException($"No value found for key '{key}' with locale '{locale ?? "default"}'.");
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllByLocaleAsync(string? locale = null)
    {
        var dictionary = await LazyLocalesTask.Value;

        foreach (var candidate in EnumerateLocaleCandidates(locale))
        {
            if (dictionary.TryGetValue(candidate, out var localizedMap))
            {
                return localizedMap;
            }
        }

        if (dictionary.TryGetValue(DefaultAliasName, out var defaultMap))
        {
            return defaultMap;
        }

        throw new KeyNotFoundException($"No values found for locale '{locale ?? "default"}'.");
    }

    private static IEnumerable<string> EnumerateLocaleCandidates(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            yield break;
        }

        var normalized = locale.Trim();
        yield return normalized;

        var separatorIndex = normalized.IndexOfAny(['-', '_']);
        if (separatorIndex > 0)
        {
            yield return normalized[..separatorIndex];
        }
    }
}

internal sealed class PersonalityTraitsHttpService(IServiceProvider serviceProvider) : HttpServiceBase(serviceProvider)
{
    protected override string Filename => "personality_traits.json";
}

internal sealed class WorkAttitudesHttpService(IServiceProvider serviceProvider) : HttpServiceBase(serviceProvider)
{
    protected override string Filename => "work_attitudes.json";
}

internal sealed class CommunicationStylesHttpService(IServiceProvider serviceProvider) : HttpServiceBase(serviceProvider)
{
    protected override string Filename => "communication_styles.json";
}
