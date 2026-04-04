using System.Net.Http.Headers;
using System.Text.Json;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Security;

namespace OpenStaff.Api.Services;

/// <summary>
/// 从各供应商 API 获取可用模型列表 / Fetches available models from provider APIs
/// </summary>
public class ModelListingService
{
    private readonly HttpClient _httpClient;
    private readonly EncryptionService _encryption;
    private readonly ILogger<ModelListingService> _logger;

    public ModelListingService(HttpClient httpClient, EncryptionService encryption, ILogger<ModelListingService> logger)
    {
        _httpClient = httpClient;
        _encryption = encryption;
        _logger = logger;
    }

    public record ModelInfo(string Id, string? DisplayName);

    /// <summary>
    /// 获取供应商的可用模型列表 / Get available models from a provider
    /// </summary>
    public async Task<List<ModelInfo>> ListModelsAsync(ModelProvider provider, CancellationToken ct = default)
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
                    // "models/gemini-2.0-flash" → "gemini-2.0-flash"
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
