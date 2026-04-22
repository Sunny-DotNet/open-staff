using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Provider.Models;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace OpenStaff.Provider.Protocols;

/// <summary>
/// Anthropic 供应商协议，基于共享目录公开 Claude 等 Messages 协议兼容模型。
/// Anthropic vendor protocol that exposes Claude-style Messages-compatible models from the shared catalog.
/// </summary>
internal class AnthropicProtocol(IServiceProvider serviceProvider) : VendorProtocolBase<AnthropicProtocolEnv>(serviceProvider)
{
    public override string ProtocolKey => "anthropic";

    public override string Logo => "Claude.Color";
    public override string ProtocolName => "Anthropic";

    public override ModelProtocolType ProtocolType => ModelProtocolType.AnthropicMessages;

    public override async Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
    {
        var httpClient = GetRequiredService<IHttpClientFactory>().CreateClient(ProtocolKey);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Env.BaseUrl!.TrimEnd('/')}/v1/models");
        request.Headers.Add("x-api-key", Env.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}");

        var payload = await response.Content.ReadFromJsonAsync<AnthropicModelsResponse>(cancellationToken);
        if (payload.Data is null)
            throw new InvalidOperationException("Anthropic 模型接口返回了空响应。");
        return payload.Data.Select(m => new ModelInfo(m.Id, ProtocolKey, ModelProtocolType.AnthropicMessages)).ToList();
    }
}

/// <summary>
/// Anthropic 协议环境配置。
/// Environment settings for the Anthropic protocol.
/// </summary>
public class AnthropicProtocolEnv : ProtocolApiKeyEnvironmentBase
{
    public override string BaseUrl { get; set; } = "https://api.anthropic.com";

    protected override string ApiKeyFromEnvDefault => "ANTHROPIC_AUTH_TOKEN";
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
