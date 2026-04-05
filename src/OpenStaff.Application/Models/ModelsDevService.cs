using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenStaff.Application.Models;

/// <summary>
/// models.dev 数据服务 — 下载并缓存 https://models.dev/api.json
/// 首次启动时下载到 ~/.staff/models-dev.json，之后每次启动异步更新
/// </summary>
public class ModelsDevService
{
    private const string RemoteUrl = "https://models.dev/api.json";
    private readonly string _localPath;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModelsDevService> _logger;

    /// <summary>
    /// providerId (如 "openai") → ProviderData
    /// </summary>
    private ConcurrentDictionary<string, ModelsDevProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;
    private readonly object _loadLock = new();

    public ModelsDevService(IHttpClientFactory httpClientFactory, ILogger<ModelsDevService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        var staffDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".staff");
        Directory.CreateDirectory(staffDir);
        _localPath = Path.Combine(staffDir, "models-dev.json");
    }

    /// <summary>
    /// 初始化：加载本地缓存，如果不存在则同步下载
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (File.Exists(_localPath))
        {
            LoadFromLocal();
            // 异步刷新（不阻塞启动）
            _ = Task.Run(() => RefreshAsync(CancellationToken.None), CancellationToken.None);
        }
        else
        {
            // 首次启动，同步下载
            await RefreshAsync(ct);
        }
    }

    /// <summary>
    /// 从远程下载并更新本地缓存
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Downloading models data from {Url}", RemoteUrl);
            using var httpClient = _httpClientFactory.CreateClient();
            var json = await httpClient.GetStringAsync(RemoteUrl, ct);
            await File.WriteAllTextAsync(_localPath, json, ct);
            ParseJson(json);
            _logger.LogInformation("Models data updated, {Count} providers loaded", _providers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download models data from {Url}", RemoteUrl);
            // 如果下载失败但有本地缓存，使用本地缓存
            if (!_loaded && File.Exists(_localPath))
            {
                LoadFromLocal();
            }
        }
    }

    /// <summary>
    /// 获取指定供应商的模型列表
    /// </summary>
    /// <param name="providerKey">供应商标识，如 "openai", "google", "anthropic", "github-copilot"</param>
    public List<ModelsDevModel> GetModels(string providerKey)
    {
        EnsureLoaded();
        if (_providers.TryGetValue(providerKey, out var provider))
        {
            return provider.Models.Values
                .OrderBy(m => m.Name ?? m.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        return [];
    }

    /// <summary>
    /// 获取指定供应商的特定模型
    /// </summary>
    public ModelsDevModel? GetModel(string providerKey, string modelId)
    {
        EnsureLoaded();
        if (_providers.TryGetValue(providerKey, out var provider)
            && provider.Models.TryGetValue(modelId, out var model))
        {
            return model;
        }
        return null;
    }

    /// <summary>
    /// 获取所有供应商标识列表
    /// </summary>
    public List<string> GetProviderKeys()
    {
        EnsureLoaded();
        return _providers.Keys.OrderBy(k => k).ToList();
    }

    /// <summary>
    /// 获取供应商信息
    /// </summary>
    public ModelsDevProvider? GetProvider(string providerKey)
    {
        EnsureLoaded();
        return _providers.TryGetValue(providerKey, out var p) ? p : null;
    }

    /// <summary>
    /// 数据是否已加载
    /// </summary>
    public bool IsLoaded => _loaded;

    /// <summary>
    /// 本地缓存文件的最后修改时间
    /// </summary>
    public DateTime? LastUpdated =>
        File.Exists(_localPath) ? File.GetLastWriteTimeUtc(_localPath) : null;

    // ===== Internal =====

    private void EnsureLoaded()
    {
        if (_loaded) return;
        lock (_loadLock)
        {
            if (_loaded) return;
            if (File.Exists(_localPath))
            {
                LoadFromLocal();
            }
        }
    }

    private void LoadFromLocal()
    {
        try
        {
            var json = File.ReadAllText(_localPath);
            ParseJson(json);
            _logger.LogInformation("Models data loaded from local cache, {Count} providers", _providers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load local models data cache");
        }
    }

    private void ParseJson(string json)
    {
        var newProviders = new ConcurrentDictionary<string, ModelsDevProvider>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            try
            {
                var providerEl = prop.Value;
                var provider = new ModelsDevProvider
                {
                    Id = providerEl.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? prop.Name : prop.Name,
                    Name = providerEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : prop.Name,
                    Api = providerEl.TryGetProperty("api", out var apiEl) ? apiEl.GetString() : null,
                    Doc = providerEl.TryGetProperty("doc", out var docEl) ? docEl.GetString() : null,
                };

                // env 数组
                if (providerEl.TryGetProperty("env", out var envEl) && envEl.ValueKind == JsonValueKind.Array)
                {
                    provider.EnvVars = envEl.EnumerateArray()
                        .Select(e => e.GetString())
                        .Where(e => e != null)
                        .Cast<string>()
                        .ToList();
                }

                // models 对象
                if (providerEl.TryGetProperty("models", out var modelsEl) && modelsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var modelProp in modelsEl.EnumerateObject())
                    {
                        var modelEl = modelProp.Value;
                        var model = ParseModel(modelProp.Name, modelEl);
                        if (model != null)
                        {
                            provider.Models[model.Id] = model;
                        }
                    }
                }

                newProviders[prop.Name] = provider;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse provider {Key}", prop.Name);
            }
        }

        _providers = newProviders;
        _loaded = true;
    }

    private static ModelsDevModel? ParseModel(string key, JsonElement el)
    {
        var model = new ModelsDevModel
        {
            Id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? key : key,
            Name = el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : key,
            Family = el.TryGetProperty("family", out var famEl) ? famEl.GetString() : null,
            Reasoning = el.TryGetProperty("reasoning", out var rEl) && rEl.ValueKind == JsonValueKind.True,
            ToolCall = el.TryGetProperty("tool_call", out var tcEl) && tcEl.ValueKind == JsonValueKind.True,
            Attachment = el.TryGetProperty("attachment", out var attEl) && attEl.ValueKind == JsonValueKind.True,
            OpenWeights = el.TryGetProperty("open_weights", out var owEl) && owEl.ValueKind == JsonValueKind.True,
            ReleaseDate = el.TryGetProperty("release_date", out var rdEl) ? rdEl.GetString() : null,
            Knowledge = el.TryGetProperty("knowledge", out var kEl) ? kEl.GetString() : null,
        };

        // limit
        if (el.TryGetProperty("limit", out var limitEl) && limitEl.ValueKind == JsonValueKind.Object)
        {
            model.ContextWindow = limitEl.TryGetProperty("context", out var cEl) && cEl.ValueKind == JsonValueKind.Number
                ? cEl.GetInt64() : null;
            model.MaxOutput = limitEl.TryGetProperty("output", out var oEl) && oEl.ValueKind == JsonValueKind.Number
                ? oEl.GetInt64() : null;
        }

        // pricing
        if (el.TryGetProperty("pricing", out var pricingEl) && pricingEl.ValueKind == JsonValueKind.Object)
        {
            model.InputPrice = pricingEl.TryGetProperty("input", out var ipEl) ? ipEl.GetString() : null;
            model.OutputPrice = pricingEl.TryGetProperty("output", out var opEl) ? opEl.GetString() : null;
        }

        // modalities
        if (el.TryGetProperty("modalities", out var modEl) && modEl.ValueKind == JsonValueKind.Object)
        {
            if (modEl.TryGetProperty("input", out var inEl) && inEl.ValueKind == JsonValueKind.Array)
            {
                model.InputModalities = inEl.EnumerateArray()
                    .Select(e => e.GetString()).Where(e => e != null).Cast<string>().ToList();
            }
            if (modEl.TryGetProperty("output", out var outEl) && outEl.ValueKind == JsonValueKind.Array)
            {
                model.OutputModalities = outEl.EnumerateArray()
                    .Select(e => e.GetString()).Where(e => e != null).Cast<string>().ToList();
            }
        }

        return model;
    }
}

// ===== Data Models =====

public class ModelsDevProvider
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Api { get; set; }
    public string? Doc { get; set; }
    public List<string> EnvVars { get; set; } = [];
    public Dictionary<string, ModelsDevModel> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class ModelsDevModel
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Family { get; set; }
    public bool Reasoning { get; set; }
    public bool ToolCall { get; set; }
    public bool Attachment { get; set; }
    public bool OpenWeights { get; set; }
    public string? ReleaseDate { get; set; }
    public string? Knowledge { get; set; }
    public long? ContextWindow { get; set; }
    public long? MaxOutput { get; set; }
    public string? InputPrice { get; set; }
    public string? OutputPrice { get; set; }
    public List<string> InputModalities { get; set; } = [];
    public List<string> OutputModalities { get; set; } = [];
}
