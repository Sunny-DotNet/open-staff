using System.Net.Http.Headers;
using System.Text.Json;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Security;

namespace OpenStaff.Api.Services;

/// <summary>
/// 模型列表服务 — 优先从 models.dev 本地缓存获取，也支持直接调用供应商 API
/// </summary>
public class ModelListingService
{
    private readonly HttpClient _httpClient;
    private readonly EncryptionService _encryption;
    private readonly ModelsDevService _modelsDev;
    private readonly ILogger<ModelListingService> _logger;

    /// <summary>
    /// ProviderType → models.dev 的 key 映射
    /// </summary>
    private static readonly Dictionary<string, string> ProviderTypeToModelsDevKey = new(StringComparer.OrdinalIgnoreCase)
    {
        [ProviderTypes.OpenAI] = "openai",
        [ProviderTypes.Google] = "google",
        [ProviderTypes.Anthropic] = "anthropic",
        [ProviderTypes.GitHubCopilot] = "github-copilot",
    };

    public ModelListingService(HttpClient httpClient, EncryptionService encryption, ModelsDevService modelsDev, ILogger<ModelListingService> logger)
    {
        _httpClient = httpClient;
        _encryption = encryption;
        _modelsDev = modelsDev;
        _logger = logger;
    }

    public record ModelInfo(string Id, string? DisplayName, long? ContextWindow = null, long? MaxOutput = null, bool? Reasoning = null);

    /// <summary>
    /// 获取供应商的可用模型列表 — 优先从 models.dev 缓存获取
    /// </summary>
    public async Task<List<ModelInfo>> ListModelsAsync(ModelProvider provider, CancellationToken ct = default)
    {
        // 1. 尝试从 models.dev 本地缓存获取
        if (_modelsDev.IsLoaded && ProviderTypeToModelsDevKey.TryGetValue(provider.ProviderType, out var devKey))
        {
            var devModels = _modelsDev.GetModels(devKey);
            if (devModels.Count > 0)
            {
                _logger.LogDebug("Returning {Count} models for {Provider} from models.dev cache", devModels.Count, provider.Name);
                return devModels.Select(m => new ModelInfo(
                    m.Id,
                    m.Name != m.Id ? m.Name : null,
                    m.ContextWindow,
                    m.MaxOutput,
                    m.Reasoning
                )).ToList();
            }
        }

        // 2. 回退：直接调用供应商 API
        _logger.LogDebug("Falling back to direct API call for {Provider}", provider.Name);
        return await ListModelsFromApiAsync(provider, ct);
    }

    /// <summary>
    /// 直接调用供应商 API 获取模型列表（需要 API Key）
    /// </summary>
    public async Task<List<ModelInfo>> ListModelsFromApiAsync(ModelProvider provider, CancellationToken ct = default)
    {
        var apiKey = ResolveApiKey(provider);
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Provider {Name} has no API key configured", provider.Name);
            return [];
        }

        try
        {
            return provider.ProviderType switch
            {
                ProviderTypes.OpenAI or ProviderTypes.GenericOpenAI or ProviderTypes.AzureOpenAI
                    => await ListOpenAiModelsAsync(provider.BaseUrl!, apiKey, ct),
                ProviderTypes.GitHubCopilot
                    => await ListOpenAiModelsAsync(provider.BaseUrl!, apiKey, ct),
                ProviderTypes.Google
                    => await ListGoogleModelsAsync(provider.BaseUrl!, apiKey, ct),
                ProviderTypes.Anthropic
                    => await ListAnthropicModelsAsync(provider.BaseUrl!, apiKey, ct),
                _ => []
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list models for provider {Name} ({Type})", provider.Name, provider.ProviderType);
            throw;
        }
    }

    /// <summary>
    /// 获取指定模型的详细参数（从 models.dev 缓存）
    /// </summary>
    public ModelsDevModel? GetModelDetails(string providerType, string modelId)
    {
        if (ProviderTypeToModelsDevKey.TryGetValue(providerType, out var devKey))
        {
            return _modelsDev.GetModel(devKey, modelId);
        }
        return null;
    }

    private string? ResolveApiKey(ModelProvider provider)
    {
        return provider.ApiKeyMode switch
        {
            ApiKeyModes.Input or ApiKeyModes.Device =>
                !string.IsNullOrEmpty(provider.ApiKeyEncrypted) ? _encryption.Decrypt(provider.ApiKeyEncrypted) : null,
            ApiKeyModes.EnvVar =>
                !string.IsNullOrEmpty(provider.ApiKeyEnvVar) ? Environment.GetEnvironmentVariable(provider.ApiKeyEnvVar) : null,
            _ => null
        };
    }

    /// <summary>
    /// OpenAI 兼容格式: GET /models → { data: [{ id: "gpt-4o" }] }
    /// </summary>
    private async Task<List<ModelInfo>> ListOpenAiModelsAsync(string baseUrl, string apiKey, CancellationToken ct)
    {
        var url = $"{baseUrl.TrimEnd('/')}/models";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var models = new List<ModelInfo>();

        if (doc.RootElement.TryGetProperty("data", out var dataArr))
        {
            foreach (var item in dataArr.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString();
                if (!string.IsNullOrEmpty(id))
                {
                    models.Add(new ModelInfo(id, null));
                }
            }
        }

        models.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
        return models;
    }

    /// <summary>
    /// Google Gemini: GET /models?key={key} → { models: [{ name: "models/gemini-2.0-flash", displayName: "..." }] }
    /// </summary>
    private async Task<List<ModelInfo>> ListGoogleModelsAsync(string baseUrl, string apiKey, CancellationToken ct)
    {
        var url = $"{baseUrl.TrimEnd('/')}/models?key={apiKey}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var models = new List<ModelInfo>();

        if (doc.RootElement.TryGetProperty("models", out var modelsArr))
        {
            foreach (var item in modelsArr.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString();
                var displayName = item.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;

                if (!string.IsNullOrEmpty(name))
                {
                    var id = name.StartsWith("models/") ? name["models/".Length..] : name;
                    models.Add(new ModelInfo(id, displayName));
                }
            }
        }

        models.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
        return models;
    }

    /// <summary>
    /// Anthropic: GET /models, x-api-key header → { data: [{ id: "claude-sonnet-4-20250514", display_name: "..." }] }
    /// </summary>
    private async Task<List<ModelInfo>> ListAnthropicModelsAsync(string baseUrl, string apiKey, CancellationToken ct)
    {
        var url = $"{baseUrl.TrimEnd('/')}/models";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var models = new List<ModelInfo>();

        if (doc.RootElement.TryGetProperty("data", out var dataArr))
        {
            foreach (var item in dataArr.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString();
                var displayName = item.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;

                if (!string.IsNullOrEmpty(id))
                {
                    models.Add(new ModelInfo(id, displayName));
                }
            }
        }

        models.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
        return models;
    }
}
