using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace OpenStaff.Application.Auth.Services;
/// <summary>
/// GitHub Copilot Token 服务 — 管理 oauth_token → github_token 的交换和缓存
/// 设备授权获取的是 oauth_token，调用 Copilot API 需要用 oauth_token 换取短期 github_token
/// </summary>
public class CopilotTokenService
{
    private const string TokenEndpoint = "https://api.github.com/copilot_internal/v2/token";
    private static readonly TimeSpan TokenRefreshBuffer = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CopilotTokenService> _logger;

    // 缓存: oauth_token hash → CopilotToken
    private readonly ConcurrentDictionary<string, CopilotToken> _tokenCache = new();

    /// <summary>
    /// Initializes the singleton token cache service with the HTTP client factory used for GitHub exchanges and the logger used to trace cache refreshes.
    /// 使用执行 GitHub 令牌交换的 HTTP 客户端工厂和记录缓存刷新的日志器初始化单例令牌缓存服务。
    /// </summary>
    /// <param name="httpClientFactory">Factory for named HTTP clients that call the Copilot token endpoint. / 创建调用 Copilot 令牌端点的命名 HTTP 客户端的工厂。</param>
    /// <param name="logger">Logger for cache hits, refreshes, and exchange failures in the singleton cache. / 用于记录单例缓存命中、刷新与交换失败的日志器。</param>
    public CopilotTokenService(IHttpClientFactory httpClientFactory, ILogger<CopilotTokenService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 获取 Copilot API Token — 自动缓存和刷新
    /// 输入: oauth_token (设备授权获取的长期 token)
    /// 输出: github_token (短期 token，用于 Copilot API 调用)
    /// </summary>
    public async Task<CopilotToken> GetTokenAsync(string oauthToken, CancellationToken ct = default)
    {
        // 用 oauth_token 的 hash 作为缓存 key（避免存储明文）
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
    /// 用 oauth_token 向 GitHub 换取 Copilot API token
    /// GET https://api.github.com/copilot_internal/v2/token
    /// Authorization: token {oauth_token}
    /// </summary>
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
            _logger.LogError("Copilot token exchange failed: {StatusCode} {Body}",
                response.StatusCode, errorBody);
            throw new InvalidOperationException(
                $"Copilot token 交换失败 (HTTP {(int)response.StatusCode}): {errorBody}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<CopilotTokenResponse>(json)
            ?? throw new InvalidOperationException("无法解析 Copilot token 响应");

        if (string.IsNullOrEmpty(result.Token))
        {
            throw new InvalidOperationException("Copilot token 响应中缺少 token 字段");
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(result.ExpiresAt).UtcDateTime;

        _logger.LogInformation("Copilot token obtained, expires at {ExpiresAt}, endpoints: {Endpoints}",
            expiresAt, result.Endpoints?.ChatCompletions ?? "default");

        return new CopilotToken
        {
            Token = result.Token,
            ExpiresAt = expiresAt,
            ChatCompletionsEndpoint = result.Endpoints?.ChatCompletions
        };
    }

    /// <summary>
    /// 清除缓存（例如 oauth_token 更新后）
    /// </summary>
    public void ClearCache()
    {
        _tokenCache.Clear();
        _logger.LogInformation("Copilot token cache cleared");
    }
}

/// <summary>
/// 已缓存的 Copilot Token
/// </summary>
public class CopilotToken
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string? ChatCompletionsEndpoint { get; set; }

    /// <summary>是否已过期（提前5分钟刷新）</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
}

#region Internal DTOs

internal class CopilotTokenResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; set; }

    [JsonPropertyName("refresh_in")]
    public int RefreshIn { get; set; }

    [JsonPropertyName("endpoints")]
    public CopilotEndpoints? Endpoints { get; set; }
}

internal class CopilotEndpoints
{
    [JsonPropertyName("api")]
    public string? Api { get; set; }

    [JsonPropertyName("proxy")]
    public string? Proxy { get; set; }

    [JsonPropertyName("chat_completions")]
    public string? ChatCompletions { get; set; }
}

#endregion

