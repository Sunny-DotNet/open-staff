using Microsoft.Extensions.Options;
using OpenStaff.Agent;
using OpenStaff.Configurations;
using OpenStaff.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenStaff.Plugin.Anthropic;

public class AnthropicPlatformConfiguration
{
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
}

public sealed class AnthropicPlatformMetadataService : VendorPlatformMetadataBase
{
    public override string ProviderType => "anthropic";

    public override string DisplayName => "Anthropic Claude";

    public override string? AvatarDataUri => "https://unpkg.com/@lobehub/icons-static-png@latest/light/anthropic.png";
}

public sealed class AnthropicConfigurationService(
    IOptions<OpenStaffOptions> openStaffOptions) : VendorConfigurationServiceBase<AnthropicPlatformConfiguration>(openStaffOptions)
{
    public override string ProviderType => "anthropic";

    public override ConfigurationProperty[] ConfigurationProperties { get; } =
    [
        new(nameof(AnthropicPlatformConfiguration.ApiKey), ConfigurationPropertyType.String, null, true),
        new(nameof(AnthropicPlatformConfiguration.BaseUrl), ConfigurationPropertyType.String, global::Anthropic.Core.EnvironmentUrl.Production, false)
    ];

    internal async Task<AnthropicPlatformConfiguration> GetNormalizedConfigurationAsync(CancellationToken ct = default)
    {
        var configuration = (await GetConfigurationAsync(ct)).Configuration;
        configuration.BaseUrl = NormalizeBaseUrl(configuration.BaseUrl);
        return configuration;
    }

    internal async Task<AnthropicPlatformConfiguration> GetEffectiveConfigurationAsync(CancellationToken ct = default)
    {
        var configuration = await GetNormalizedConfigurationAsync(ct);
        if (string.IsNullOrWhiteSpace(configuration.ApiKey))
            throw new InvalidOperationException("请先在 Anthropic 提供程序配置中填写 API Key");

        return configuration;
    }

    private static string NormalizeBaseUrl(string? baseUrl)
        => string.IsNullOrWhiteSpace(baseUrl)
            ? global::Anthropic.Core.EnvironmentUrl.Production
            : baseUrl;
}

public sealed class AnthropicModelCatalogService(
    AnthropicConfigurationService configurationService,
    IHttpClientFactory httpClientFactory) : VendorModelCatalogServiceBase
{
    protected override async Task<IReadOnlyList<VendorModel>> GetModelsAsync(CancellationToken ct = default)
    {
        var configuration = await configurationService.GetEffectiveConfigurationAsync(ct);
        return await FetchModelsAsync(configuration, ct);
    }

    public override async Task<VendorModelCatalogResult> GetModelCatalogAsync(CancellationToken ct = default)
    {
        var configuration = await configurationService.GetNormalizedConfigurationAsync(ct);
        if (string.IsNullOrWhiteSpace(configuration.ApiKey))
        {
            return VendorModelCatalogResult.RequiresProviderConfiguration(
                "请先在 Provider 配置中填写 API Key，保存后再加载 Anthropic 模型列表。",
                nameof(AnthropicPlatformConfiguration.ApiKey));
        }

        try
        {
            return VendorModelCatalogResult.Ready(await FetchModelsAsync(configuration, ct));
        }
        catch (HttpRequestException ex)
        {
            return VendorModelCatalogResult.LoadFailed($"Anthropic 模型列表加载失败：{ex.Message}");
        }
        catch (JsonException ex)
        {
            return VendorModelCatalogResult.LoadFailed($"Anthropic 模型列表解析失败：{ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return VendorModelCatalogResult.LoadFailed($"Anthropic 模型列表加载失败：{ex.Message}");
        }
    }

    private async Task<IReadOnlyList<VendorModel>> FetchModelsAsync(AnthropicPlatformConfiguration configuration, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{configuration.BaseUrl!.TrimEnd('/')}/v1/models");
        request.Headers.Add("x-api-key", configuration.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await httpClientFactory.CreateClient(configurationService.ProviderType).SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}");

        var payload = await response.Content.ReadFromJsonAsync<AnthropicModelsResponse>(cancellationToken: ct);
        if (payload.Data is null)
            throw new InvalidOperationException("Anthropic 模型接口返回了空响应。");

        return payload.Data.Select(m => new VendorModel(m.Id, m.DisplayName)).ToList();
    }
}

internal record struct AnthropicModelsResponse(
    [property: JsonPropertyName("data")] AnthropicModelData[] Data,
    [property: JsonPropertyName("first_id")] string? FirstId,
    [property: JsonPropertyName("last_id")] string? LastId,
    [property: JsonPropertyName("has_more")] bool HasMore);

internal record struct AnthropicModelData(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("type")] string Type);
