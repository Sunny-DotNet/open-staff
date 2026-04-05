using System.Collections.Concurrent;
using System.Text.Json;

namespace OpenStaff.Plugins.ModelDataSource;

/// <summary>
/// models.dev 数据源实现 — 从 https://models.dev/api.json 获取模型元数据
/// 支持本地缓存（~/.staff/models-dev.json），首次同步下载，之后异步刷新
/// </summary>
public class ModelsDevModelDataSource : IModelDataSource
{
    private const string RemoteUrl = "https://models.dev/api.json";

    private readonly HttpClient _httpClient;
    private readonly string _localPath;

    private ConcurrentDictionary<string, ModelVendor> _vendors = new(StringComparer.OrdinalIgnoreCase);
    private ConcurrentDictionary<string, List<ModelData>> _modelsByVendor = new(StringComparer.OrdinalIgnoreCase);
    private List<ModelData> _allModels = [];
    private bool _ready;
    private readonly object _lock = new();

    public string SourceId => "models.dev";
    public string DisplayName => "Models.dev";
    public bool IsReady => _ready;
    public DateTime? LastUpdatedUtc => File.Exists(_localPath) ? File.GetLastWriteTimeUtc(_localPath) : null;

    public ModelsDevModelDataSource(HttpClient? httpClient = null, string? cachePath = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        var staffDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".staff");
        Directory.CreateDirectory(staffDir);
        _localPath = cachePath ?? Path.Combine(staffDir, "models-dev.json");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_localPath))
        {
            LoadFromLocal();
            _ = Task.Run(() => RefreshAsync(CancellationToken.None), CancellationToken.None);
        }
        else
        {
            await RefreshAsync(cancellationToken);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _httpClient.GetStringAsync(RemoteUrl, cancellationToken);
            await File.WriteAllTextAsync(_localPath, json, cancellationToken);
            ParseJson(json);
        }
        catch
        {
            if (!_ready && File.Exists(_localPath))
                LoadFromLocal();
        }
    }

    public Task<IReadOnlyList<ModelVendor>> GetVendorsAsync(CancellationToken cancellationToken = default)
    {
        EnsureReady();
        return Task.FromResult<IReadOnlyList<ModelVendor>>(
            _vendors.Values.OrderBy(v => v.Name).ToList());
    }

    public Task<IReadOnlyList<ModelData>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        EnsureReady();
        return Task.FromResult<IReadOnlyList<ModelData>>(_allModels);
    }

    public Task<IReadOnlyList<ModelData>> GetModelsByVendorAsync(string vendorId, CancellationToken cancellationToken = default)
    {
        EnsureReady();
        if (_modelsByVendor.TryGetValue(vendorId, out var models))
            return Task.FromResult<IReadOnlyList<ModelData>>(models);
        return Task.FromResult<IReadOnlyList<ModelData>>([]);
    }

    public Task<ModelData?> GetModelAsync(string vendorId, string modelId, CancellationToken cancellationToken = default)
    {
        EnsureReady();
        if (_modelsByVendor.TryGetValue(vendorId, out var models))
        {
            var match = models.FirstOrDefault(m => m.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<ModelData?>(match);
        }
        return Task.FromResult<ModelData?>(null);
    }

    // ===== Internal =====

    private void EnsureReady()
    {
        if (_ready) return;
        lock (_lock)
        {
            if (_ready) return;
            if (File.Exists(_localPath))
                LoadFromLocal();
        }
    }

    private void LoadFromLocal()
    {
        try
        {
            var json = File.ReadAllText(_localPath);
            ParseJson(json);
        }
        catch { /* 缓存损坏时静默忽略 */ }
    }

    private void ParseJson(string json)
    {
        var vendors = new ConcurrentDictionary<string, ModelVendor>(StringComparer.OrdinalIgnoreCase);
        var modelsByVendor = new ConcurrentDictionary<string, List<ModelData>>(StringComparer.OrdinalIgnoreCase);
        var allModels = new List<ModelData>();

        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            try
            {
                var vendorId = prop.Name;
                var el = prop.Value;

                // 解析供应商
                var vendor = ParseVendor(vendorId, el);
                vendors[vendorId] = vendor;

                // 解析模型
                var vendorModels = new List<ModelData>();
                if (el.TryGetProperty("models", out var modelsEl) && modelsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var modelProp in modelsEl.EnumerateObject())
                    {
                        var model = ParseModel(vendorId, modelProp.Name, modelProp.Value);
                        if (model != null)
                            vendorModels.Add(model);
                    }
                }

                vendorModels.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                modelsByVendor[vendorId] = vendorModels;
                allModels.AddRange(vendorModels);
            }
            catch { /* 单个供应商解析失败不影响整体 */ }
        }

        allModels.Sort((a, b) =>
        {
            var c = string.Compare(a.VendorId, b.VendorId, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        _vendors = vendors;
        _modelsByVendor = modelsByVendor;
        _allModels = allModels;
        _ready = true;
    }

    private static ModelVendor ParseVendor(string vendorId, JsonElement el)
    {
        var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? vendorId : vendorId;
        var name = el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? vendorId : vendorId;
        var api = el.TryGetProperty("api", out var apiEl) ? apiEl.GetString() : null;
        var doc = el.TryGetProperty("doc", out var docEl) ? docEl.GetString() : null;

        var envVars = new List<string>();
        if (el.TryGetProperty("env", out var envEl) && envEl.ValueKind == JsonValueKind.Array)
        {
            envVars = envEl.EnumerateArray()
                .Select(e => e.GetString())
                .Where(e => e != null)
                .Cast<string>()
                .ToList();
        }

        return new ModelVendor(id, name, api, doc, envVars);
    }

    private static ModelData? ParseModel(string vendorId, string key, JsonElement el)
    {
        var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? key : key;
        var name = el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? key : key;
        var family = el.TryGetProperty("family", out var famEl) ? famEl.GetString() : null;

        // 发布日期
        DateTime? releasedAt = null;
        if (el.TryGetProperty("release_date", out var rdEl) && rdEl.GetString() is string rdStr
            && DateTime.TryParse(rdStr, out var dt))
        {
            releasedAt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        // 模态
        var inputModalities = ModelModality.None;
        var outputModalities = ModelModality.None;
        if (el.TryGetProperty("modalities", out var modEl) && modEl.ValueKind == JsonValueKind.Object)
        {
            if (modEl.TryGetProperty("input", out var inEl) && inEl.ValueKind == JsonValueKind.Array)
                inputModalities = ParseModalities(inEl);
            if (modEl.TryGetProperty("output", out var outEl) && outEl.ValueKind == JsonValueKind.Array)
                outputModalities = ParseModalities(outEl);
        }

        // 能力
        var caps = ModelCapability.None;
        if (el.TryGetProperty("reasoning", out var rEl) && rEl.ValueKind == JsonValueKind.True)
            caps |= ModelCapability.Reasoning;
        if (el.TryGetProperty("tool_call", out var tcEl) && tcEl.ValueKind == JsonValueKind.True)
            caps |= ModelCapability.FunctionCall;
        if (el.TryGetProperty("structured_output", out var soEl) && soEl.ValueKind == JsonValueKind.True)
            caps |= ModelCapability.JsonMode;
        if (el.TryGetProperty("attachment", out var attEl) && attEl.ValueKind == JsonValueKind.True)
            caps |= ModelCapability.Attachment;
        if (el.TryGetProperty("open_weights", out var owEl) && owEl.ValueKind == JsonValueKind.True)
            caps |= ModelCapability.OpenWeights;
        if (inputModalities.HasFlag(ModelModality.Image))
            caps |= ModelCapability.Vision;

        // 限制
        long? contextWindow = null, maxInput = null, maxOutput = null;
        if (el.TryGetProperty("limit", out var limitEl) && limitEl.ValueKind == JsonValueKind.Object)
        {
            contextWindow = limitEl.TryGetProperty("context", out var cEl) && cEl.ValueKind == JsonValueKind.Number ? cEl.GetInt64() : null;
            maxInput = limitEl.TryGetProperty("input", out var miEl) && miEl.ValueKind == JsonValueKind.Number ? miEl.GetInt64() : null;
            maxOutput = limitEl.TryGetProperty("output", out var moEl) && moEl.ValueKind == JsonValueKind.Number ? moEl.GetInt64() : null;
        }

        // 价格
        string? inputPrice = null, outputPrice = null, cacheRead = null, cacheWrite = null;
        if (el.TryGetProperty("pricing", out var pricingEl) && pricingEl.ValueKind == JsonValueKind.Object)
        {
            inputPrice = pricingEl.TryGetProperty("input", out var ipEl) ? ipEl.GetString() : null;
            outputPrice = pricingEl.TryGetProperty("output", out var opEl) ? opEl.GetString() : null;
            cacheRead = pricingEl.TryGetProperty("cache_read", out var crEl) ? crEl.GetString() : null;
            cacheWrite = pricingEl.TryGetProperty("cache_write", out var cwEl) ? cwEl.GetString() : null;
        }

        return new ModelData(
            id, name, vendorId, family, releasedAt,
            inputModalities, outputModalities, caps,
            new ModelLimits(contextWindow, maxInput, maxOutput),
            new ModelPricing(inputPrice, outputPrice, cacheRead, cacheWrite));
    }

    private static ModelModality ParseModalities(JsonElement arr)
    {
        var result = ModelModality.None;
        foreach (var item in arr.EnumerateArray())
        {
            var val = item.GetString()?.ToLowerInvariant();
            result |= val switch
            {
                "text" => ModelModality.Text,
                "image" => ModelModality.Image,
                "audio" => ModelModality.Audio,
                "video" => ModelModality.Video,
                "file" => ModelModality.File,
                "embeddings" or "embedding" => ModelModality.Embeddings,
                _ => ModelModality.Other
            };
        }
        return result;
    }
}
