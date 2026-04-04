using Microsoft.Extensions.Logging;
using OpenStaff.Core.Models;
using OpenStaff.Core.Services;

namespace OpenStaff.Infrastructure.LLM;

/// <summary>
/// 模型客户端工厂 / Model client factory implementation
/// </summary>
public class ModelClientFactory : IModelClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public ModelClientFactory(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    public IModelClient CreateClient(ModelProvider provider)
    {
        var httpClient = _httpClientFactory.CreateClient($"llm-{provider.Id}");
        var logger = _loggerFactory.CreateLogger<OpenAICompatibleClient>();

        // 所有类型都使用 OpenAI 兼容接口 / All types use OpenAI-compatible interface
        var baseUrl = provider.ProviderType switch
        {
            ProviderTypes.OpenAI => provider.BaseUrl ?? "https://api.openai.com",
            ProviderTypes.AzureOpenAI => provider.BaseUrl ?? throw new ArgumentException("Azure OpenAI 需要配置 BaseUrl"),
            ProviderTypes.GenericOpenAI => provider.BaseUrl ?? throw new ArgumentException("通用供应商需要配置 BaseUrl"),
            _ => throw new ArgumentException($"不支持的供应商类型: {provider.ProviderType}")
        };

        // TODO: 解密 API Key / Decrypt API key
        var apiKey = provider.ApiKeyEncrypted;

        return new OpenAICompatibleClient(httpClient, apiKey, baseUrl, logger);
    }
}
