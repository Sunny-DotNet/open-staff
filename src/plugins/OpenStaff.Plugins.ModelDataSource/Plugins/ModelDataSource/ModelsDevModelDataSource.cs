using System.Collections.Concurrent;
using System.Text.Json;

namespace OpenStaff.Plugins.ModelDataSource;

/// <summary>
/// models.dev 数据源实现，提供带本地缓存与异步刷新的模型元数据读取能力。
/// models.dev data source implementation that provides model metadata with local caching and asynchronous refresh behavior.
/// </summary>
public class ModelsDevModelDataSource : IModelDataSource
{
    private const string RemoteUrl = "https://models.dev/api.json";

    private readonly HttpClient _httpClient;
    private readonly string _localPath;
    private readonly object _lock = new();

    private ConcurrentDictionary<string, ModelVendor> _vendors = new(StringComparer.OrdinalIgnoreCase);
    private ConcurrentDictionary<string, List<ModelData>> _modelsByVendor = new(StringComparer.OrdinalIgnoreCase);
    private List<ModelData> _allModels = [];
    private bool _ready;

    /// <summary>
    /// 初始化 models.dev 数据源。
    /// Initializes the models.dev data source.
    /// </summary>
    /// <param name="httpClient">
    /// 可选的 HTTP 客户端。
    /// Optional HTTP client.
    /// </param>
    /// <param name="cachePath">
    /// 可选的缓存文件路径。
    /// Optional cache file path.
    /// </param>
    public ModelsDevModelDataSource(HttpClient? httpClient = null, string? cachePath = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        var staffDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".staff");
        Directory.CreateDirectory(staffDir);
        _localPath = cachePath ?? Path.Combine(staffDir, "models-dev.json");
    }

    /// <inheritdoc />
    public string SourceId => "models.dev";

    /// <inheritdoc />
    public string DisplayName => "Models.dev";

    /// <inheritdoc />
    public bool IsReady => _ready;

    /// <inheritdoc />
    public DateTime? LastUpdatedUtc => File.Exists(_localPath) ? File.GetLastWriteTimeUtc(_localPath) : null;

    /// <summary>
    /// 初始化数据源：若存在本地缓存则立即加载，并在后台异步拉取最新快照；首次启动则直接执行远程刷新。
    /// Initializes the data source by loading the local cache immediately when available and refreshing the latest snapshot in the background; on first startup it performs a remote refresh directly.
    /// </summary>
    /// <param name="cancellationToken">
    /// 取消初始化流程的令牌。
    /// Token used to cancel the initialization flow.
    /// </param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_localPath))
        {
            // zh-CN: 启动时优先加载本地缓存，让依赖方尽快可用；后台再异步刷新最新数据。
            // en: Prefer loading the local cache on startup so dependents become available quickly, then refresh the latest data in the background.
            LoadFromLocal();
            _ = Task.Run(() => RefreshAsync(CancellationToken.None), CancellationToken.None);
        }
        else
        {
            await RefreshAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 从 models.dev 下载最新 JSON 快照、更新本地缓存，并在必要时回退到已有缓存保持可用性。
    /// Downloads the latest JSON snapshot from models.dev, updates the local cache, and falls back to any existing cache when necessary to stay available.
    /// </summary>
    /// <param name="cancellationToken">
    /// 取消刷新流程的令牌。
    /// Token used to cancel the refresh flow.
    /// </param>
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
            // zh-CN: 在线刷新失败时，如果之前已有本地缓存且当前尚未就绪，则回退到缓存保证服务可用。
            // en: If the online refresh fails and the source is not ready yet, fall back to the local cache so the service can still operate.
            if (!_ready && File.Exists(_localPath))
                LoadFromLocal();
        }
    }

    /// <summary>
    /// 返回已缓存的供应商列表，并按名称排序以便 UI 与选择逻辑获得稳定输出。
    /// Returns the cached vendor list sorted by name so UI and selection logic receive stable output.
    /// </summary>
    /// <param name="cancellationToken">
    /// 取消读取操作的令牌。
    /// Token used to cancel the read operation.
    /// </param>
    /// <returns>
    /// 当前缓存中的全部供应商。
    /// All vendors currently held in the cache.
    /// </returns>
    public Task<IReadOnlyList<ModelVendor>> GetVendorsAsync(CancellationToken cancellationToken = default)
    {
        EnsureReady();
        return Task.FromResult<IReadOnlyList<ModelVendor>>(_vendors.Values.OrderBy(v => v.Name).ToList());
    }

    /// <summary>
    /// 返回跨供应商聚合后的完整模型列表。
    /// Returns the complete model list aggregated across all vendors.
    /// </summary>
    /// <param name="cancellationToken">
    /// 取消读取操作的令牌。
    /// Token used to cancel the read operation.
    /// </param>
    /// <returns>
    /// 当前缓存中的全部模型。
    /// All models currently held in the cache.
    /// </returns>
    public Task<IReadOnlyList<ModelData>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        EnsureReady();
        return Task.FromResult<IReadOnlyList<ModelData>>(_allModels);
    }

    /// <summary>
    /// 按供应商标识读取模型列表，并在供应商不存在时返回空集合而不是 <see langword="null" />。
    /// Reads the model list for a vendor and returns an empty collection instead of <see langword="null" /> when the vendor is missing.
    /// </summary>
    /// <param name="vendorId">
    /// 要查询的供应商标识。
    /// Vendor identifier to query.
    /// </param>
    /// <param name="cancellationToken">
    /// 取消读取操作的令牌。
    /// Token used to cancel the read operation.
    /// </param>
    /// <returns>
    /// 指定供应商的模型集合。
    /// Model collection for the specified vendor.
    /// </returns>
    public Task<IReadOnlyList<ModelData>> GetModelsByVendorAsync(string vendorId, CancellationToken cancellationToken = default)
    {
        EnsureReady();
        if (_modelsByVendor.TryGetValue(vendorId, out var models))
            return Task.FromResult<IReadOnlyList<ModelData>>(models);

        return Task.FromResult<IReadOnlyList<ModelData>>([]);
    }

    /// <summary>
    /// 在指定供应商的索引内执行大小写不敏感的模型查找。
    /// Performs a case-insensitive model lookup within the index for the specified vendor.
    /// </summary>
    /// <param name="vendorId">
    /// 供应商标识。
    /// Vendor identifier.
    /// </param>
    /// <param name="modelId">
    /// 模型标识。
    /// Model identifier.
    /// </param>
    /// <param name="cancellationToken">
    /// 取消读取操作的令牌。
    /// Token used to cancel the read operation.
    /// </param>
    /// <returns>
    /// 匹配的模型；未找到时返回 <see langword="null" />。
    /// Matching model, or <see langword="null" /> when no model matches.
    /// </returns>
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

    /// <summary>
    /// 在首次读取前确保缓存已加载，使调用方即使未显式初始化也能读取到本地数据。
    /// Ensures the cache is loaded before the first read so callers can still retrieve local data even if initialization was not invoked explicitly.
    /// </summary>
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

    /// <summary>
    /// 从磁盘缓存加载并解析最新快照；若缓存损坏则静默忽略，等待后续在线刷新恢复。
    /// Loads and parses the latest snapshot from disk cache; if the cache is corrupt it is ignored so a later online refresh can recover.
    /// </summary>
    private void LoadFromLocal()
    {
        try
        {
            var json = File.ReadAllText(_localPath);
            ParseJson(json);
        }
        catch
        {
            // zh-CN: 缓存损坏时静默忽略，让后续在线刷新有机会重新恢复数据。
            // en: Ignore corrupt cache files so a later online refresh still has a chance to recover the data.
        }
    }

    /// <summary>
    /// 解析 models.dev 原始 JSON，并以一次性替换的方式重建供应商索引、按供应商模型索引和全量模型列表。
    /// Parses the raw models.dev JSON and rebuilds the vendor index, per-vendor model index, and full model list as one atomic replacement.
    /// </summary>
    /// <param name="json">
    /// 从远程接口或本地缓存读取到的 JSON 文本。
    /// JSON text read from the remote endpoint or local cache.
    /// </param>
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

                // zh-CN: 顶层对象按供应商分组，因此先解析供应商，再把其下属模型聚合到同一索引中。
                // en: The top-level payload is grouped by vendor, so parse the vendor first and then aggregate its child models into the same index.
                var vendor = ParseVendor(vendorId, el);
                vendors[vendorId] = vendor;

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
            catch
            {
                // zh-CN: 单个供应商解析失败不会中断整体加载，避免上游数据中的局部异常拖垮全量同步。
                // en: Parsing failures for a single vendor do not stop the whole load so isolated upstream data issues cannot break the full sync.
            }
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

    /// <summary>
    /// 解析单个供应商节点，并在缺失字段时回退到顶层供应商键保证标识稳定。
    /// Parses a single vendor node and falls back to the top-level vendor key when fields are missing so identifiers remain stable.
    /// </summary>
    /// <param name="vendorId">
    /// 顶层供应商键。
    /// Top-level vendor key.
    /// </param>
    /// <param name="el">
    /// 供应商节点的 JSON 元素。
    /// JSON element for the vendor node.
    /// </param>
    /// <returns>
    /// 规范化后的供应商信息。
    /// Normalized vendor information.
    /// </returns>
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

    /// <summary>
    /// 将单个 models.dev 模型节点映射为统一模型记录，并折叠发布日期、模态、能力、限制与价格字段。
    /// Maps a single models.dev model node into the unified model record, collapsing release date, modalities, capabilities, limits, and pricing fields.
    /// </summary>
    /// <param name="vendorId">
    /// 所属供应商标识。
    /// Owning vendor identifier.
    /// </param>
    /// <param name="key">
    /// 模型在供应商节点中的原始键名。
    /// Raw model key name within the vendor node.
    /// </param>
    /// <param name="el">
    /// 模型节点的 JSON 元素。
    /// JSON element for the model node.
    /// </param>
    /// <returns>
    /// 规范化后的模型信息；若数据不足以构建记录则返回 <see langword="null" />。
    /// Normalized model information, or <see langword="null" /> when the data is insufficient to build a record.
    /// </returns>
    private static ModelData? ParseModel(string vendorId, string key, JsonElement el)
    {
        var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? key : key;
        var name = el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? key : key;
        var family = el.TryGetProperty("family", out var famEl) ? famEl.GetString() : null;

        // zh-CN: 发布日期按 UTC 解析，保证不同来源读取到的时间语义一致。
        // en: Parse release dates as UTC so time semantics remain consistent across different consumers.
        DateTime? releasedAt = null;
        if (el.TryGetProperty("release_date", out var rdEl) && rdEl.GetString() is string rdStr
            && DateTime.TryParse(rdStr, out var dt))
        {
            releasedAt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        // zh-CN: 输入输出模态分别读取，再组合成位标志，便于 Provider 端快速过滤支持能力。
        // en: Read input and output modalities separately and then combine them into flags so provider code can filter capabilities efficiently.
        var inputModalities = ModelModality.None;
        var outputModalities = ModelModality.None;
        if (el.TryGetProperty("modalities", out var modEl) && modEl.ValueKind == JsonValueKind.Object)
        {
            if (modEl.TryGetProperty("input", out var inEl) && inEl.ValueKind == JsonValueKind.Array)
                inputModalities = ParseModalities(inEl);
            if (modEl.TryGetProperty("output", out var outEl) && outEl.ValueKind == JsonValueKind.Array)
                outputModalities = ParseModalities(outEl);
        }

        // zh-CN: 把离散的布尔字段归并为统一能力位标志，减少上层消费者对原始 JSON 结构的耦合。
        // en: Collapse discrete Boolean fields into a unified capability bit field so upper layers do not need to couple to the raw JSON shape.
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

        var contextWindow = (long?)null;
        var maxInput = (long?)null;
        var maxOutput = (long?)null;
        if (el.TryGetProperty("limit", out var limitEl) && limitEl.ValueKind == JsonValueKind.Object)
        {
            contextWindow = limitEl.TryGetProperty("context", out var cEl) && cEl.ValueKind == JsonValueKind.Number ? cEl.GetInt64() : null;
            maxInput = limitEl.TryGetProperty("input", out var miEl) && miEl.ValueKind == JsonValueKind.Number ? miEl.GetInt64() : null;
            maxOutput = limitEl.TryGetProperty("output", out var moEl) && moEl.ValueKind == JsonValueKind.Number ? moEl.GetInt64() : null;
        }

        var inputPrice = (string?)null;
        var outputPrice = (string?)null;
        var cacheRead = (string?)null;
        var cacheWrite = (string?)null;
        if (el.TryGetProperty("pricing", out var pricingEl) && pricingEl.ValueKind == JsonValueKind.Object)
        {
            inputPrice = pricingEl.TryGetProperty("input", out var ipEl) ? ipEl.GetString() : null;
            outputPrice = pricingEl.TryGetProperty("output", out var opEl) ? opEl.GetString() : null;
            cacheRead = pricingEl.TryGetProperty("cache_read", out var crEl) ? crEl.GetString() : null;
            cacheWrite = pricingEl.TryGetProperty("cache_write", out var cwEl) ? cwEl.GetString() : null;
        }

        return new ModelData(
            id,
            name,
            vendorId,
            family,
            releasedAt,
            inputModalities,
            outputModalities,
            caps,
            new ModelLimits(contextWindow, maxInput, maxOutput),
            new ModelPricing(inputPrice, outputPrice, cacheRead, cacheWrite));
    }

    /// <summary>
    /// 将 models.dev 的字符串模态数组转换为位标志，并把未知值归入 <see cref="ModelModality.Other" /> 以保留信号。
    /// Converts the models.dev string modality array into bit flags and routes unknown values to <see cref="ModelModality.Other" /> so the signal is preserved.
    /// </summary>
    /// <param name="arr">
    /// 模态字符串数组。
    /// Array of modality strings.
    /// </param>
    /// <returns>
    /// 组合后的模态位标志。
    /// Combined modality flags.
    /// </returns>
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
