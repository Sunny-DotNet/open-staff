using Microsoft.Extensions.Logging;
using OpenStaff.Provider.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OpenStaff.Provider.Protocols;

/// <summary>
/// GitHub Copilot 协议 — 通过 device-auth 获取 OAuth Token，再换取 Copilot API Token
/// 非供应商协议，模型列表通过 Copilot API 动态获取
/// </summary>
internal class GitHubCopilotProtocol(
    IServiceProvider serviceProvider,
    CopilotTokenService copilotTokenService,
    IHttpClientFactory httpClientFactory) : ProtocolBase<GitHubCopilotProtocolEnv>(serviceProvider)
{
    private const string DefaultModelsEndpoint = "https://api.githubcopilot.com/models";

    public override bool IsVendor => false;

    public override string ProviderName => "Github Copilot";

    public override string ProviderKey => "github-copilot";

    public override string Logo => "GithubCopilot";

    public override async Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(Env?.OAuthToken))
        {
            Logger.LogWarning("GitHub Copilot OAuthToken 未配置，无法获取模型列表");
            return [];
        }

        // 1. 用 OAuthToken 换取短期 github_token
        var copilotToken = await copilotTokenService.GetTokenAsync(Env.OAuthToken, cancellationToken);

        // 2. 用 github_token 调用 /models 端点
        var modelsUrl = DefaultModelsEndpoint;
        // 如果 token 响应包含自定义 API endpoint，则用它
        if (!string.IsNullOrEmpty(copilotToken.ChatCompletionsEndpoint))
        {
            var baseUri = new Uri(copilotToken.ChatCompletionsEndpoint);
            modelsUrl = $"{baseUri.Scheme}://{baseUri.Host}/models";
        }

        var httpClient = httpClientFactory.CreateClient("copilot-models");
        using var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", copilotToken.Token);
        request.Headers.UserAgent.TryParseAdd("GitHubCopilotChat/1.0.102");
        request.Headers.Add("Editor-Version", "vscode/1.100.0");
        request.Headers.Add("Editor-Plugin-Version", "copilot-chat/0.27.0");
        request.Headers.Add("Copilot-Integration-Id", "vscode-chat");

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Logger.LogError("获取 Copilot 模型列表失败: URL={Url} {StatusCode} {Body}", modelsUrl, response.StatusCode, errorBody);
            throw new HttpRequestException($"Copilot models API 请求失败 (HTTP {(int)response.StatusCode}): {errorBody}");
        }

        // 3. 解析 OpenAI 兼容格式 { data: [{ id: "..." }] }
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var models = new List<ModelInfo>();

        if (doc.RootElement.TryGetProperty("data", out var dataArr))
        {
            foreach (var item in dataArr.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                if (string.IsNullOrEmpty(id)) continue;

                models.Add(new ModelInfo(
                    ModelSlug: id,
                    VenderSlug: "github-copilot",
                    ModelProtocols: ModelProtocolType.OpenAIChatCompletions
                ));
            }
        }

        Logger.LogInformation("获取到 {Count} 个 Copilot 模型", models.Count);
        return models;
    }
}

public class GitHubCopilotProtocolEnv : ProtocolEnvBase
{
    public override string BaseUrl { get; set; } = "https://api.individual.githubcopilot.com";
    public string OAuthToken { get; set; } = string.Empty;
}
