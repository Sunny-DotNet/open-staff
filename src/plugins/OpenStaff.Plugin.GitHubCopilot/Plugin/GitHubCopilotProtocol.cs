using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Serialization;
using OpenStaff.Configurations;
using OpenStaff.Net;
using OpenStaff.Options;
using OpenStaff.Plugin.Models;
using OpenStaff.Plugin.Services;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Protocols;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenStaff.Plugin;

/// <summary>
/// GitHub Copilot 协议，通过 OAuth token 换取 Copilot API token 后动态拉取模型列表。
/// GitHub Copilot protocol that exchanges an OAuth token for a Copilot API token and then fetches the model catalog dynamically.
/// </summary>
/// <param name="serviceProvider">
/// 用于解析工作目录选项与日志依赖的服务提供程序。
/// Service provider used to resolve working-directory options and logging dependencies.
/// </param>
/// <param name="copilotTokenService">
/// 负责将 GitHub OAuth token 交换为 Copilot API token 的服务。
/// Service that exchanges a GitHub OAuth token for a Copilot API token.
/// </param>
/// <param name="httpClientFactory">
/// 用于创建 Copilot 模型目录请求客户端的工厂。
/// Factory used to create HTTP clients for the Copilot model catalog request.
/// </param>
internal class GitHubCopilotProtocol(
    IServiceProvider serviceProvider,
    CopilotTokenService copilotTokenService,
    IHttpClientFactory httpClientFactory) : ProtocolBase<GitHubCopilotProtocolEnv>(serviceProvider)
{
    private const string DefaultModelsEndpoint = "https://api.githubcopilot.com/models";

    public override bool IsVendor => false;

    public override string ProtocolName => "GitHub Copilot";

    public override string ProtocolKey => "github-copilot";

    public override string Logo => "GitHubCopilot";

    /// <summary>
    /// 换取 Copilot API token、拉取模型目录并将支持端点映射为内部协议位标志。
    /// Exchanges for a Copilot API token, fetches the model catalog, and maps supported endpoints to internal protocol flags.
    /// </summary>
    /// <param name="cancellationToken">
    /// 取消整个 Copilot 模型发现流程的令牌。
    /// Token used to cancel the full Copilot model-discovery flow.
    /// </param>
    /// <returns>
    /// 当前账号可访问且可映射到已知协议类型的模型集合。
    /// Collection of models accessible to the current account that can be mapped to known protocol types.
    /// </returns>
    public override async Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(Env?.OAuthToken))
        {
            Logger.LogWarning("GitHub Copilot OAuthToken 未配置，无法获取模型列表");
            return [];
        }

        var options = GetRequiredService<IOptions<OpenStaffOptions>>();
        var cachedModelFilePath = Path.Combine(options.Value.WorkingDirectory, "providers", "github_copilot_models.json");

        // zh-CN: 先用长期 OAuth token 换取短期 Copilot API token，这是调用模型目录接口的前置条件。
        // en: Exchange the long-lived OAuth token for a short-lived Copilot API token before calling the model catalog endpoint.
        var copilotToken = await copilotTokenService.GetTokenAsync(Env.OAuthToken, cancellationToken);

        var modelsUrl = DefaultModelsEndpoint;
        if (!string.IsNullOrEmpty(copilotToken.ChatCompletionsEndpoint))
        {
            var baseUri = new Uri(copilotToken.ChatCompletionsEndpoint);
            modelsUrl = $"{baseUri.Scheme}://{baseUri.Host}/models";
        }

        // zh-CN: 模型目录接口会返回 OpenAI 风格的 data 数组，这里先落盘缓存，再统一从缓存文件反序列化。
        // en: The model catalog endpoint returns an OpenAI-style data array, so cache it on disk first and then deserialize from the cached file consistently.
        await DownloadHelper.DownloadUseCachedAsync(
            modelsUrl,
            cachedModelFilePath,
            TimeSpan.FromDays(1),
            () => 
            {
                var client = httpClientFactory.CreateClient("copilot-models");
                GitHubCopilotHttpClientHelper.ConfigureHttpClient(client);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", copilotToken.Token);
                return client;
            },
            cancellationToken);

        using var file = File.OpenRead(cachedModelFilePath);
        var response = await JsonSerializer.DeserializeAsync<GitHubCopilotModelListResponse>(file, cancellationToken: cancellationToken);
        var models = new List<ModelInfo>();

        foreach (var item in response.Data)
        {
            var id = item.Id;
            var vendor = item.Vendor;
            var modelProtocols = ModelProtocolType.None;

            if (item.SupportedEndpoints?.Count > 0)
            {
                foreach (var supportedEndpoint in item.SupportedEndpoints)
                {
                    if (EndpointToModelProtocolType(supportedEndpoint) is { } protocol)
                        modelProtocols |= protocol;
                }
            }

            if (modelProtocols > ModelProtocolType.None)
                models.Add(new ModelInfo(id, vendor ?? "github-copilot", modelProtocols));
        }

        Logger.LogInformation("获取到 {Count} 个 Copilot 模型", models.Count);
        return models;
    }

    /// <summary>
    /// 将 Copilot 返回的端点路径转换为内部协议标志；未知端点会被忽略以避免声明错误能力。
    /// Converts a Copilot endpoint path into an internal protocol flag; unknown endpoints are ignored to avoid advertising unsupported capabilities.
    /// </summary>
    /// <param name="endpoint">
    /// Copilot 模型声明的支持端点路径。
    /// Supported endpoint path declared by a Copilot model.
    /// </param>
    /// <returns>
    /// 匹配到的协议标志；若当前系统尚不识别该端点则返回 <see langword="null" />。
    /// Matching protocol flag, or <see langword="null" /> when the endpoint is not recognized by the current system.
    /// </returns>
    private static ModelProtocolType? EndpointToModelProtocolType(string endpoint) => endpoint.ToLower() switch
    {
        "/chat/completions" => ModelProtocolType.OpenAIChatCompletions,
        "/responses" => ModelProtocolType.OpenAIResponse,
        "/v1/messages" => ModelProtocolType.AnthropicMessages,
        "/generatecontent" => ModelProtocolType.GoogleGenerateContent,
        _ => null
    };

}

/// <summary>
/// GitHub Copilot 协议环境配置。
/// Environment settings for the GitHub Copilot protocol.
/// </summary>


public class GitHubCopilotProtocolEnv : ProtocolEnvBase
{
    /// <inheritdoc />
    [Description("Base URL for the GitHub Copilot API.")]
    public override string BaseUrl { get; set; } = "https://api.individual.githubcopilot.com";

    /// <summary>
    /// GitHub device flow 获取的 OAuth token。
    /// OAuth token obtained through the GitHub device flow.
    /// </summary>
    [Encrypted]
    public string OAuthToken { get; set; } = string.Empty;
}
