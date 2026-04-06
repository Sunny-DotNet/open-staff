using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider.Models;

namespace OpenStaff.Provider.Protocols;

/// <summary>
/// NewApi/OneAPI 兼容协议 — OpenAI 兼容的 API 网关
/// 从 {BaseUrl}/api/pricing 获取网关配置的模型列表，
/// 通过 vendor + model_name 与 IModelDataSource 匹配获取完整元数据
/// </summary>
public class NewApiProtocol(IServiceProvider serviceProvider) : ProtocolBase<NewApiProtocolEnv>(serviceProvider)
{
    private static readonly HttpClient SharedHttpClient = new();

    public override bool IsVendor => false;

    public override string ProviderName => "newapi";

    public override async Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
    {
        if (Env is null || string.IsNullOrWhiteSpace(Env.BaseUrl))
        {
            Logger.LogWarning("NewApi BaseUrl 未配置，跳过模型获取");
            return [];
        }

        var pricingUrl = $"{Env.BaseUrl.TrimEnd('/')}/api/pricing";

        try
        {
            var json = await SharedHttpClient.GetStringAsync(pricingUrl, cancellationToken);
            return await ParsePricingAsync(json, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "从 NewApi 获取 pricing 失败: {Url}", pricingUrl);
            return [];
        }
    }

    private async Task<IEnumerable<ModelInfo>> ParsePricingAsync(string json, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("success", out var successEl) || !successEl.GetBoolean())
            return [];

        // 1. 映射 supported_endpoint → ModelProtocolType
        var endpointMap = new Dictionary<string, ModelProtocolType>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("supported_endpoint", out var sepEl) && sepEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var ep in sepEl.EnumerateObject())
            {
                var protocolType = MapEndpointType(ep.Name);
                if (protocolType != 0)
                    endpointMap[ep.Name] = protocolType;
            }
        }

        // 2. 构建 vendor_id → vendor_name 映射
        var vendorMap = new Dictionary<int, string>();
        if (root.TryGetProperty("vendors", out var vendorsEl) && vendorsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in vendorsEl.EnumerateArray())
            {
                var id = v.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32() : 0;
                var name = v.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                if (id > 0 && !string.IsNullOrEmpty(name))
                    vendorMap[id] = name;
            }
        }

        // 3. 预加载 IModelDataSource 供应商，建立 显示名 → vendorId 的索引
        var vendorNameIndex = await BuildVendorNameIndexAsync(cancellationToken);

        // 4. 遍历 data，匹配模型
        var results = new List<ModelInfo>();
        if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataEl.EnumerateArray())
            {
                var modelName = item.TryGetProperty("model_name", out var mnEl) ? mnEl.GetString() : null;
                if (string.IsNullOrEmpty(modelName)) continue;

                var vendorId = item.TryGetProperty("vendor_id", out var vidEl) && vidEl.ValueKind == JsonValueKind.Number
                    ? vidEl.GetInt32() : 0;

                // 合并该模型支持的所有 endpoint 类型
                var protocols = ModelProtocolType.OpenAIChatCompletions; // NewApi 至少支持 OpenAI 兼容
                if (item.TryGetProperty("supported_endpoint_types", out var etsEl) && etsEl.ValueKind == JsonValueKind.Array)
                {
                    protocols = 0;
                    foreach (var et in etsEl.EnumerateArray())
                    {
                        var etName = et.GetString();
                        if (etName != null && endpointMap.TryGetValue(etName, out var pt))
                            protocols |= pt;
                    }
                    if (protocols == 0)
                        protocols = ModelProtocolType.OpenAIChatCompletions;
                }

                // 通过 NewApi vendor_name → IModelDataSource vendorId 映射
                var vendorSlug = ResolveVendorSlug(vendorId, vendorMap, vendorNameIndex);

                // 尝试从 IModelDataSource 精确匹配
                if (vendorSlug != null)
                {
                    var matched = await ModelDataSource.GetModelAsync(vendorSlug, modelName, cancellationToken);
                    if (matched != null)
                    {
                        results.Add(new ModelInfo(matched.Id, VendorReplace(matched.VendorId), protocols));
                        continue;
                    }
                }

                // 未匹配到 IModelDataSource，使用 NewApi 原始数据
                results.Add(new ModelInfo(modelName, vendorSlug ?? "unknown", protocols));
            }
        }

        return results;
    }

    private string VendorReplace(string vendor) => vendor.ToLower() switch {
        "zhipuai-coding-plan"=>"zai",
        _ =>vendor
    }; 




    /// <summary>
    /// 构建 IModelDataSource 供应商的 名称 → vendorId 索引
    /// 支持按 Name（大小写不敏感）和 Id 双向匹配
    /// </summary>
    private async Task<Dictionary<string, string>> BuildVendorNameIndexAsync(CancellationToken cancellationToken)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!ModelDataSource.IsReady) return index;

        var vendors = await ModelDataSource.GetVendorsAsync(cancellationToken);
        foreach (var v in vendors)
        {
            // Id 自身（如 "zhipu"）
            index.TryAdd(v.Id, v.Id);
            // 显示名（如 "Zhipu"）
            index.TryAdd(v.Name, v.Id);
        }

        return index;
    }

    /// <summary>
    /// 将 NewApi 的 vendor_id + vendor_name 映射到 IModelDataSource 的 vendorId
    /// </summary>
    private static string? ResolveVendorSlug(
        int newApiVendorId,
        Dictionary<int, string> vendorMap,
        Dictionary<string, string> vendorNameIndex)
    {
        if (!vendorMap.TryGetValue(newApiVendorId, out var vendorName))
            return null;

        // 直接名称匹配
        if (vendorNameIndex.TryGetValue(vendorName, out var slug))
            return slug;

        // 模糊匹配：检查 vendorNameIndex 中是否有 key 包含 vendorName 或反之
        foreach (var (key, value) in vendorNameIndex)
        {
            if (key.Contains(vendorName, StringComparison.OrdinalIgnoreCase) ||
                vendorName.Contains(key, StringComparison.OrdinalIgnoreCase))
                return value;
        }

        return null;
    }

    private static ModelProtocolType MapEndpointType(string endpointName)
    {
        return endpointName.ToLowerInvariant() switch
        {
            "openai" or "chat/completions" => ModelProtocolType.OpenAIChatCompletions,
            "openai-response" or "responses" => ModelProtocolType.OpenAIResponse,
            "anthropic" or "messages" => ModelProtocolType.AnthropicMessages,
            "google" or "gemini" or "generatecontent" => ModelProtocolType.GoogleGenerateContent,
            _ => ModelProtocolType.OpenAIChatCompletions // NewApi 默认 OpenAI 兼容
        };
    }
}

public class NewApiProtocolEnv : ProtocolEnvBase
{
    public string BaseUrl { get; set; } = string.Empty;
    public bool FromEnv { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
    public string EnvName { get; set; } = string.Empty;
}