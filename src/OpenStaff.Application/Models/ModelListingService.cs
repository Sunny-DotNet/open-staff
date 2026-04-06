using System.Net.Http.Headers;
using System.Text.Json;
using OpenStaff.Core.Models;
using OpenStaff.Application.Providers;
using OpenStaff.Infrastructure.Security;
using OpenStaff.Plugins.ModelDataSource;

namespace OpenStaff.Application.Models;

/// <summary>
/// 模型列表服务 — 优先从 IModelDataSource 获取，也支持直接调用供应商 API
/// </summary>
public class ModelListingService
{
    private readonly HttpClient _httpClient;
    private readonly EncryptionService _encryption;
    private readonly ProviderAccountService _accountService;
    private readonly IModelDataSource _dataSource;
    private readonly ILogger<ModelListingService> _logger;

    /// <summary>
    /// ProtocolType → models.dev 的 vendor key 映射
    /// </summary>
    private static readonly Dictionary<string, string> ProtocolToVendorKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openai"] = "openai",
        ["google"] = "google",
        ["anthropic"] = "anthropic",
        ["github-copilot"] = "github-copilot",
    };

    public ModelListingService(HttpClient httpClient, EncryptionService encryption, ProviderAccountService accountService, IModelDataSource dataSource, ILogger<ModelListingService> logger)
    {
        _httpClient = httpClient;
        _encryption = encryption;
        _accountService = accountService;
        _dataSource = dataSource;
        _logger = logger;
    }

    public record ModelInfo(string Id, string? DisplayName, long? ContextWindow = null, long? MaxOutput = null, bool? Reasoning = null);

    /// <summary>
    /// 获取供应商账户的可用模型列表 — 优先从数据源缓存获取
    /// </summary>
    public async Task<List<ModelInfo>> ListModelsAsync(ProviderAccount account, CancellationToken ct = default)
    {
        // 1. 尝试从数据源获取
        if (_dataSource.IsReady && ProtocolToVendorKey.TryGetValue(account.ProtocolType, out var vendorKey))
        {
            var models = await _dataSource.GetModelsByVendorAsync(vendorKey, ct);
            if (models.Count > 0)
            {
                _logger.LogDebug("Returning {Count} models for {Account} from {Source}", models.Count, account.Name, _dataSource.SourceId);
                return models.Select(m => new ModelInfo(
                    m.Id,
                    m.Name != m.Id ? m.Name : null,
                    m.Limits.ContextWindow,
                    m.Limits.MaxOutput,
                    m.Capabilities.HasFlag(ModelCapability.Reasoning) ? true : null
                )).ToList();
            }
        }

        // 2. 回退：直接调用供应商 API
        _logger.LogDebug("Falling back to direct API call for {Account}", account.Name);
        return await ListModelsFromApiAsync(account, ct);
    }

    /// <summary>
    /// 直接调用供应商 API 获取模型列表（需要 API Key）
    /// </summary>
    public async Task<List<ModelInfo>> ListModelsFromApiAsync(ProviderAccount account, CancellationToken ct = default)
    {
        var envConfig = _accountService.GetEnvConfigDict(account);
        var apiKey = GetEnvString(envConfig, "ApiKey");
        var baseUrl = GetEnvString(envConfig, "BaseUrl");

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Account {Name} has no API key configured", account.Name);
            return [];
        }

        try
        {
            return account.ProtocolType switch
            {
                "openai" or "newapi" => await ListOpenAiModelsAsync(baseUrl!, apiKey, ct),
                "github-copilot" => await ListOpenAiModelsAsync("https://api.individual.githubcopilot.com", apiKey, ct),
                "google" => await ListGoogleModelsAsync(baseUrl!, apiKey, ct),
                "anthropic" => await ListAnthropicModelsAsync(baseUrl!, apiKey, ct),
                _ => []
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list models for account {Name} ({Protocol})", account.Name, account.ProtocolType);
            throw;
        }
    }

    /// <summary>
    /// 获取指定模型的详细参数（从数据源缓存）
    /// </summary>
    public async Task<ModelData?> GetModelDetailsAsync(string protocolType, string modelId, CancellationToken ct = default)
    {
        if (ProtocolToVendorKey.TryGetValue(protocolType, out var vendorKey))
        {
            return await _dataSource.GetModelAsync(vendorKey, modelId, ct);
        }
        return null;
    }

    private static string? GetEnvString(Dictionary<string, object?>? config, string key)
    {
        if (config == null) return null;
        return config.TryGetValue(key, out var val) ? val?.ToString() : null;
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
                    models.Add(new ModelInfo(id, null));
            }
        }

        models.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
        return models;
    }

    /// <summary>
    /// Google Gemini: GET /models?key={key}
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
    /// Anthropic: GET /models, x-api-key header
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
                    models.Add(new ModelInfo(id, displayName));
            }
        }

        models.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
        return models;
    }
}
