using Anthropic;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenStaff.Infrastructure.Security;
using OpenStaff.Options;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Protocols;
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Environment = System.Environment;

namespace OpenStaff.Provider.Platforms;

public interface IChatClientFactory
{
    Task<IChatClient> CreateAsync(ChatClientCreateRequest request, CancellationToken cancellationToken = default);
}
public sealed record ChatClientCreateRequest(string AccountId,string ModelId);

public abstract class ChatClientFactoryBase(IServiceProvider serviceProvider) :ServiceBase(serviceProvider), IChatClientFactory
{

    protected virtual bool GetUrlSkipVersionLabel() => false;
    public virtual async Task<IChatClient> CreateAsync(ChatClientCreateRequest request, CancellationToken cancellationToken = default)
    {
        var model = await GetModelAsync(request.ModelId);
        var modelProtocol = GetModelProtocol(model.ModelProtocols);
        var chatClient = await CreateChatClientAsync(modelProtocol, request);
        return chatClient;
    }

    protected abstract Task<IChatClient> CreateChatClientAsync(ModelProtocolType modelProtocol, ChatClientCreateRequest request);

    protected virtual async Task<ModelInfo> GetModelAsync(string modelId)
    {
        
        throw new NotImplementedException();
    }
    protected virtual ModelProtocolType GetModelProtocol(ModelProtocolType modelProtocols)
    {
        if (modelProtocols.HasFlag(ModelProtocolType.OpenAIResponse))
            return ModelProtocolType.OpenAIResponse;
        if (modelProtocols.HasFlag(ModelProtocolType.AnthropicMessages))
            return ModelProtocolType.AnthropicMessages;
        if (modelProtocols.HasFlag(ModelProtocolType.GoogleGenerateContent))
            return ModelProtocolType.GoogleGenerateContent;
        if (modelProtocols.HasFlag(ModelProtocolType.OpenAIChatCompletions))
            return ModelProtocolType.OpenAIChatCompletions;
        return ModelProtocolType.None;
    }



    /// <summary>
    /// 创建 Google Generate Content 聊天客户端；默认会把常见的 <c>/v1beta2</c> 后缀裁剪回服务根。
    /// Creates a Google Generate Content chat client; by default it trims the common <c>/v1beta2</c> suffix back to the service root.
    /// </summary>
    /// <param name="model">
    /// 模型元数据。
    /// Model metadata.
    /// </param>
    /// <returns>
    /// Google Generate Content 聊天客户端。
    /// Google Generate Content chat client.
    /// </returns>
    protected virtual IChatClient CreateGoogleGenerateContentChatClient(string modelId, string apiKey, string? baseUrl = null)
    {
        HttpOptions? httpOptions = null;
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = baseUrl.TrimEnd('/');
            if (baseUrl.EndsWith("/v1beta2", StringComparison.OrdinalIgnoreCase))
                baseUrl = baseUrl[..^8];

            httpOptions = new() { BaseUrl = baseUrl };
        }
        var client = new Client(vertexAI: false, apiKey: apiKey, httpOptions: httpOptions);
        return client.AsIChatClient(modelId);
    }

    /// <summary>
    /// 创建 Anthropic Messages 聊天客户端。
    /// Creates an Anthropic Messages chat client.
    /// </summary>
    /// <param name="model">
    /// 模型元数据。
    /// Model metadata.
    /// </param>
    /// <returns>
    /// Anthropic Messages 聊天客户端。
    /// Anthropic Messages chat client.
    /// </returns>
    protected virtual IChatClient CreateAnthropicMessagesChatClient(string modelId, string apiKey, string? baseUrl = null)
    {
        var clientOptions = new Anthropic.Core.ClientOptions()
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl ?? Anthropic.Core.EnvironmentUrl.Production,
            HttpClient=GetRequiredService<IHttpClientFactory>().CreateClient()
        };
        ConfigureHttpClient(clientOptions.HttpClient);
        var client = new AnthropicClient(clientOptions);
        return client.AsIChatClient(modelId);
    }

    protected virtual void ConfigureHttpClient(HttpClient httpClient) { }

    /// <summary>
    /// 创建 OpenAI Responses 聊天客户端。
    /// Creates an OpenAI Responses chat client.
    /// </summary>
    /// <param name="model">
    /// 模型元数据。
    /// Model metadata.
    /// </param>
    /// <param name="provider">
    /// 已解析的 provider 账号信息。
    /// Resolved provider account information.
    /// </param>
    /// <returns>
    /// OpenAI Responses 聊天客户端。
    /// OpenAI Responses chat client.
    /// </returns>
    protected virtual IChatClient CreateOpenAIResponseChatClient(string modelId, string apiKey, string? baseUrl = null)
    {
        var options = OpenAICompatibleChatClientFactorySupport.CreateOpenAIClientOptions(OpenAICompatibleChatClientFactorySupport.NormalizeOpenAIBaseUrl(baseUrl, GetUrlSkipVersionLabel()));
        ConfigureOpenAIClientOptions(options);

        var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);

#pragma warning disable OPENAI001
        return client.GetResponsesClient().AsIChatClient(modelId);
#pragma warning restore OPENAI001
    }

    protected virtual void ConfigureOpenAIClientOptions(OpenAIClientOptions options) { }
    /// <summary>
    /// 创建 OpenAI Chat Completions 聊天客户端。
    /// Creates an OpenAI Chat Completions chat client.
    /// </summary>
    /// <param name="model">
    /// 模型元数据。
    /// Model metadata.
    /// </param>
    /// <param name="provider">
    /// 已解析的 provider 账号信息。
    /// Resolved provider account information.
    /// </param>
    /// <returns>
    /// OpenAI Chat Completions 聊天客户端。
    /// OpenAI Chat Completions chat client.
    /// </returns>
    protected virtual IChatClient CreateOpenAICompatibleChatClient(string modelId, string apiKey, string? baseUrl = null)
    {
        var options = OpenAICompatibleChatClientFactorySupport.CreateOpenAIClientOptions(OpenAICompatibleChatClientFactorySupport.NormalizeOpenAIBaseUrl(baseUrl, GetUrlSkipVersionLabel()));
        ConfigureOpenAIClientOptions(options);
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        var chatClient = client.GetChatClient(modelId).AsIChatClient();
        return chatClient;
    }

}


public abstract class DefaultChatClientFactoryBase(IServiceProvider serviceProvider) : ChatClientFactoryBase(serviceProvider)
{

    protected override async Task<IChatClient> CreateChatClientAsync(ModelProtocolType modelProtocol, ChatClientCreateRequest request)
    {
        var environment=await LoadConfigurationAsync<DefaultProtocolApiKeyEnvironment>(request.AccountId);
        if(environment==null)
            throw new InvalidOperationException($"API key environment configuration for account '{request.AccountId}' could not be loaded.");

        var apiKey = string.Empty;
        if(environment.ApiKeyFromEnv)
            apiKey = Environment.GetEnvironmentVariable(environment.ApiKeyEnvName) ?? throw new InvalidOperationException($"API key environment variable '{environment.ApiKeyEnvName}' is not set.");
        else
            apiKey = environment.ApiKey ?? throw new InvalidOperationException("API key value is not provided in the configuration.");     
        var baseUrl = environment.BaseUrl;
        return modelProtocol switch
        {
            ModelProtocolType.OpenAIChatCompletions => CreateOpenAICompatibleChatClient(request.ModelId, apiKey, baseUrl),
            ModelProtocolType.OpenAIResponse => CreateOpenAIResponseChatClient(request.ModelId, apiKey, baseUrl),
            ModelProtocolType.AnthropicMessages => CreateAnthropicMessagesChatClient(request.ModelId, apiKey, baseUrl),
            ModelProtocolType.GoogleGenerateContent => CreateGoogleGenerateContentChatClient(request.ModelId, apiKey, baseUrl),
            _ => throw new NotSupportedException($"Model protocol {modelProtocol} is not supported by {GetType().Name}.")
        };
    }

    protected async Task<T?> LoadConfigurationAsync<T>(string accountId)
        where T : class
    {
        var filename = Path.Combine(GetRequiredService<IOptions<OpenStaffOptions>>().Value.WorkingDirectory, "providers", $"{accountId}.json");
        if (System.IO.File.Exists(filename))
        {

            var json = await System.IO.File.ReadAllTextAsync(filename);
            var configuration = ProtocolEnvSerializer.Deserialize<T>(json, DecryptFunc) ?? default;
            return configuration;
        }
        return default;

    }

    private string DecryptFunc(string arg)
    {
        var isDevelopment = GetService<IHostEnvironment>()?.IsDevelopment() == true;
        if (isDevelopment) return arg;
        var encryption = GetService<EncryptionService>();
        return encryption?.Encrypt(arg) ?? arg;
    }
}
