using Microsoft.Extensions.Logging;
using OpenStaff.Provider.Models;
using System.Text.Json;

namespace OpenStaff.Provider.Protocols;

/// <summary>
/// NewApi/OneAPI 兼容协议，从兼容网关的 pricing 接口推导模型列表。
/// NewApi/OneAPI compatible protocol that derives model metadata from a gateway pricing endpoint.
/// </summary>
public class NewApiProtocol(IServiceProvider serviceProvider) : ProtocolBase<NewApiProtocolEnv>(serviceProvider)
{
    private static readonly HttpClient SharedHttpClient = new();

    public override bool IsVendor => false;

    public override string ProtocolName => "New API";

    public override string ProtocolKey => "newapi";

    public override string Logo => string.Empty;

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

        var vendorMap = new Dictionary<int, string>();
        if (root.TryGetProperty("vendors", out var vendorsEl) && vendorsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in vendorsEl.EnumerateArray())
            {
                var id = v.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32() : 0;
                var name = v.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
                if (id > 0 && !string.IsNullOrEmpty(name))
                    vendorMap[id] = name;
            }
        }

        var vendorNameIndex = await BuildVendorNameIndexAsync(cancellationToken);
        var results = new List<ModelInfo>();
        if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataEl.EnumerateArray())
            {
                var modelName = item.TryGetProperty("model_name", out var mnEl) ? mnEl.GetString() : null;
                if (string.IsNullOrEmpty(modelName))
                    continue;

                var vendorId = item.TryGetProperty("vendor_id", out var vidEl) && vidEl.ValueKind == JsonValueKind.Number
                    ? vidEl.GetInt32()
                    : 0;

                var protocols = ModelProtocolType.OpenAIChatCompletions;
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

                var vendorSlug = ResolveVendorSlug(vendorId, vendorMap, vendorNameIndex);
                if (vendorSlug != null)
                {
                    var matched = await ModelDataSource.GetModelAsync(vendorSlug, modelName, cancellationToken);
                    if (matched != null)
                    {
                        results.Add(new ModelInfo(matched.Id, VendorReplace(matched.VendorId), protocols));
                        continue;
                    }
                }
                if(vendorSlug == null) Console.WriteLine(  );
                results.Add(new ModelInfo(modelName, vendorSlug ?? "unknown", protocols));
            }
        }

        return results;
    }

    private string VendorReplace(string vendor) => vendor.ToLower() switch
    {
        "zhipuai-coding-plan" => "zai",
        _ => vendor
    };

    private async Task<Dictionary<string, string>> BuildVendorNameIndexAsync(CancellationToken cancellationToken)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!ModelDataSource.IsReady)
            return index;

        var vendors = await ModelDataSource.GetVendorsAsync(cancellationToken);
        foreach (var v in vendors)
        {
            index.TryAdd(v.Id, v.Id);
            index.TryAdd(v.Name, v.Id);
        }

        return index;
    }

    private static string? ResolveVendorSlug(
        int newApiVendorId,
        Dictionary<int, string> vendorMap,
        Dictionary<string, string> vendorNameIndex)
    {
        if (!vendorMap.TryGetValue(newApiVendorId, out var vendorName))
            return null;

        if (vendorNameIndex.TryGetValue(vendorName, out var slug))
            return slug;

        foreach (var pair in vendorNameIndex)
        {
            if (pair.Key.Contains(vendorName, StringComparison.OrdinalIgnoreCase)
                || vendorName.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static ModelProtocolType MapEndpointType(string endpointType) => endpointType.ToLowerInvariant() switch
    {
        "openai" or "chat/completions" => ModelProtocolType.OpenAIChatCompletions,
        "openai-response" or "responses" => ModelProtocolType.OpenAIResponse,
        "anthropic" or "messages" => ModelProtocolType.AnthropicMessages,
        "google" or "gemini" or "generatecontent" => ModelProtocolType.GoogleGenerateContent,
        _ => ModelProtocolType.OpenAIChatCompletions
    };
}

public class NewApiProtocolEnv : ProtocolApiKeyEnvironmentBase
{
    public override string BaseUrl { get; set; } = string.Empty;

    protected override string ApiKeyFromEnvDefault => "NEW_API_AUTH_TOKEN";
}
