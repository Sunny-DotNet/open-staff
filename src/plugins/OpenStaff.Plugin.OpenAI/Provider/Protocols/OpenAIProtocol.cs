using Microsoft.Extensions.Options;
using OpenStaff.Net;
using OpenStaff.Options;
using OpenStaff.Provider.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenStaff.Provider.Protocols;

/// <summary>
/// OpenAI 供应商协议，复用共享供应商目录并同时暴露 Chat Completions 与 Responses 两类兼容能力。
/// OpenAI vendor protocol that reuses the shared vendor catalog and exposes both Chat Completions and Responses compatibility.
/// </summary>
internal class OpenAIProtocol(IServiceProvider serviceProvider) : VendorProtocolBase<OpenAIProtocolEnv>(serviceProvider)
{
    public override string ProtocolKey => "openai";

    public override string Logo => "OpenAI";
    public override string ProtocolName => "OpenAI";

    public override ModelProtocolType ProtocolType => ModelProtocolType.OpenAIChatCompletions | ModelProtocolType.OpenAIResponse;
    public override async Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
    {
        if (Env is null
            || string.IsNullOrWhiteSpace(Env.BaseUrl)
            || OpenAIProtocolEnv.DefaultBaseUrl.Equals(Env.BaseUrl, StringComparison.OrdinalIgnoreCase))
            return await base.ModelsAsync(cancellationToken);

        var providerDetail = GetRequiredService<ICurrentProviderDetail>().Current;
        var url = Env.BaseUrl ?? OpenAIProtocolEnv.DefaultBaseUrl;
        url = url!.TrimEnd('/');
        if (!Env.UrlSkipVersionLabel)
            url += url.EndsWith("/v1") ? string.Empty : "/v1";
        url += "/models";
        var httpClient = GetRequiredService<IHttpClientFactory>().CreateClient(url);
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {Env.ApiKey}");


        var json = string.Empty;

        if (providerDetail == null)
        {
            json=await httpClient.GetStringAsync(url, cancellationToken);
        }
        else {

            var filename = Path.Combine(
                GetRequiredService<IOptions<OpenStaffOptions>>().Value.WorkingDirectory,
                "providers",
                $"models_{providerDetail.AccountId}.json"
                );
            await DownloadHelper.DownloadUseCachedAsync(url, filename, TimeSpan.FromDays(1), () => httpClient, cancellationToken);
            json=await File.ReadAllTextAsync(filename, cancellationToken);

        }




        try
        {            
            var response= JsonSerializer.Deserialize<OpenAIModelResponse>(json);
            return response.Data.Select(m => new ModelInfo(m.Id,m.OwnedBy??"openai", ModelProtocolType.OpenAIChatCompletions)) ?? [];
        }
        catch (Exception e)
        {

        }
        return [];
    }
}

/// <summary>
/// OpenAI 协议环境配置。
/// Environment settings for the OpenAI protocol.
/// </summary>
public class OpenAIProtocolEnv : ProtocolApiKeyEnvironmentBase
{

    internal const string DefaultBaseUrl = "https://api.openai.com/v1";
    public override string? BaseUrl { get; set; } = DefaultBaseUrl;

    protected override string ApiKeyFromEnvDefault => "OPENAI_API_KEY";
    public bool UrlSkipVersionLabel { get; set; } = false;

}

public record struct OpenAIModelResponse(
    [property: JsonPropertyName("data")] OpenAIModelData[] Data,
    [property: JsonPropertyName("object")] string Object);
public record struct OpenAIModelData(
    [property: JsonPropertyName("created")] long? Created,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("context_length")] long? ContextLength,
    [property: JsonPropertyName("max_output_tokens")] long? MaxOutputTokens,
    [property: JsonPropertyName("owned_by")] string? OwnedBy
    );
