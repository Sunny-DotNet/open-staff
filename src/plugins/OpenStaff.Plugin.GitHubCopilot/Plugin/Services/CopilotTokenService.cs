using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenStaff.Plugin.Services;

/// <summary>
/// GitHub Copilot token 服务，负责 oauth_token 到 Copilot API token 的交换与缓存。
/// GitHub Copilot token service that exchanges oauth_token values for Copilot API tokens and caches the results.
/// </summary>
internal class CopilotTokenService
{
    private const string TokenEndpoint = "https://api.github.com/copilot_internal/v2/token";
    internal static readonly TimeSpan TokenRefreshBuffer = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CopilotTokenService> _logger;

    // zh-CN: 缓存键使用 oauth_token 的哈希摘要，既避免重复换取 token，也不在内存中长期保留明文凭据。
    // en: Cache keys use a hash of the oauth_token so repeated exchanges are avoided without retaining plaintext credentials in memory.
    private readonly ConcurrentDictionary<string, CopilotToken> _tokenCache = new();

    /// <summary>
    /// 初始化 Copilot token 服务。
    /// Initializes the Copilot token service.
    /// </summary>
    /// <param name="httpClientFactory">
    /// 用于创建 GitHub API 客户端的工厂。
    /// Factory used to create GitHub API clients.
    /// </param>
    /// <param name="logger">
    /// 服务日志记录器。
    /// Logger for the service.
    /// </param>
    public CopilotTokenService(IHttpClientFactory httpClientFactory, ILogger<CopilotTokenService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 获取 Copilot API token，并在可用时复用缓存。
    /// Gets a Copilot API token and reuses a cached one when available.
    /// </summary>
    /// <param name="oauthToken">
    /// 设备授权流程返回的 OAuth token。
    /// OAuth token returned by the device authorization flow.
    /// </param>
    /// <param name="ct">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// 可用于调用 Copilot API 的短期 token。
    /// Short-lived token that can be used for Copilot API calls.
    /// </returns>
    public async Task<CopilotToken> GetTokenAsync(string oauthToken, CancellationToken ct = default)
    {
        var cacheKey = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(oauthToken)))[..16];

        if (_tokenCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
        {
            _logger.LogDebug("Using cached Copilot token, expires at {ExpiresAt}", cached.ExpiresAt);
            return cached;
        }

        _logger.LogInformation("Exchanging oauth_token for Copilot API token");

        var token = await ExchangeTokenAsync(oauthToken, ct);
        _tokenCache[cacheKey] = token;
        return token;
    }

    /// <summary>
    /// 清除当前缓存的所有 token。
    /// Clears all currently cached tokens.
    /// </summary>
    public void ClearCache()
    {
        _tokenCache.Clear();
        _logger.LogInformation("Copilot token cache cleared");
    }

    /// <summary>
    /// 调用 GitHub 内部 token 接口，把设备流 OAuth token 换成可访问 Copilot API 的短期 token。
    /// Calls GitHub's internal token endpoint to exchange a device-flow OAuth token for a short-lived token that can access the Copilot API.
    /// </summary>
    /// <param name="oauthToken">
    /// GitHub 设备授权流程得到的 OAuth token。
    /// OAuth token obtained from the GitHub device authorization flow.
    /// </param>
    /// <param name="ct">
    /// 取消本次交换请求的令牌。
    /// Token used to cancel the exchange request.
    /// </param>
    /// <returns>
    /// 包含访问 token、过期时间与推荐端点的 Copilot token 结果。
    /// Copilot token result containing the access token, expiration time, and suggested endpoint information.
    /// </returns>
    private async Task<CopilotToken> ExchangeTokenAsync(string oauthToken, CancellationToken ct)
    {
        var httpClient = _httpClientFactory.CreateClient("copilot-token");

        var request = new HttpRequestMessage(HttpMethod.Get, TokenEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", oauthToken);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.TryParseAdd("GitHubCopilotChat/1.0.102");

        var response = await httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Copilot token exchange failed: {StatusCode} {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException(
                $"Copilot token 交换失败 (HTTP {(int)response.StatusCode}): {errorBody}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<CopilotInternalTokenResponse>(json)
            ?? throw new InvalidOperationException("无法解析 Copilot token 响应");

        if (string.IsNullOrEmpty(result.Token))
            throw new InvalidOperationException("Copilot token 响应中缺少 token 字段");

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(result.ExpiresAt).UtcDateTime;

        string? endpoint= null;
        if(result.Endpoints != null)
        {
            if (result.Endpoints.TryGetValue("api", out var api))
            {
                endpoint = api;
            }
        }
        _logger.LogInformation(
            "Copilot token obtained, expires at {ExpiresAt}, endpoints: {Endpoints}",
            expiresAt,
            endpoint ?? "default");

        return new CopilotToken
        {
            Token = result.Token,
            ExpiresAt = expiresAt,
            ChatCompletionsEndpoint = endpoint
        };
    }
}

/// <summary>
/// 缓存中的 Copilot API token。
/// Cached Copilot API token.
/// </summary>
public class CopilotToken
{
    /// <summary>
    /// Copilot API 访问 token。
    /// Access token for the Copilot API.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Token 过期时间（UTC）。
    /// Expiration time of the token in UTC.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Copilot 返回的 Chat Completions 端点。
    /// Chat Completions endpoint returned by Copilot.
    /// </summary>
    public string? ChatCompletionsEndpoint { get; set; }

    /// <summary>
    /// 指示 token 是否已接近过期并需要刷新。
    /// Indicates whether the token is close enough to expiration that it should be refreshed.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt - CopilotTokenService.TokenRefreshBuffer;
}

#region Internal DTOs

public record CopilotInternalTokenResponse(
    [property: JsonPropertyName("agent_mode_auto_approval")] bool AgentModeAutoApproval,
    [property: JsonPropertyName("annotations_enabled")] bool AnnotationsEnabled,
    [property: JsonPropertyName("azure_only")] bool AzureOnly,
    [property: JsonPropertyName("blackbird_clientside_indexing")] bool BlackbirdClientsideIndexing,
    [property: JsonPropertyName("chat_enabled")] bool ChatEnabled,
    [property: JsonPropertyName("chat_jetbrains_enabled")] bool ChatJetbrainsEnabled,
    [property: JsonPropertyName("code_quote_enabled")] bool CodeQuoteEnabled,
    [property: JsonPropertyName("code_review_enabled")] bool CodeReviewEnabled,
    [property: JsonPropertyName("codesearch")] bool Codesearch,
    [property: JsonPropertyName("copilotignore_enabled")] bool CopilotignoreEnabled,
    [property: JsonPropertyName("endpoints")] Dictionary<string, string> Endpoints,
    [property: JsonPropertyName("expires_at")] int ExpiresAt,
    [property: JsonPropertyName("individual")] bool Individual,
    [property: JsonPropertyName("limited_user_quotas")] object LimitedUserQuotas,
    [property: JsonPropertyName("limited_user_reset_date")] object LimitedUserResetDate,
    [property: JsonPropertyName("prompt_8k")] bool Prompt8k,
    [property: JsonPropertyName("public_suggestions")] string PublicSuggestions,
    [property: JsonPropertyName("refresh_in")] int RefreshIn,
    [property: JsonPropertyName("sku")] string Sku,
    [property: JsonPropertyName("snippy_load_test_enabled")] bool SnippyLoadTestEnabled,
    [property: JsonPropertyName("telemetry")] string Telemetry,
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("tracking_id")] string TrackingId,
    [property: JsonPropertyName("vsc_electron_fetcher_v2")] bool VscElectronFetcherV2,
    [property: JsonPropertyName("xcode")] bool Xcode,
    [property: JsonPropertyName("xcode_chat")] bool XcodeChat
    );
#endregion