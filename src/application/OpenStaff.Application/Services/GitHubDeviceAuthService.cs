using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

using OpenStaff.Application.Providers.Services;
using OpenStaff.Infrastructure.Security;

namespace OpenStaff.Application.Auth.Services;
/// <summary>
/// GitHub 设备码授权服务。
/// GitHub device-code authorization service.
/// </summary>
public class GitHubDeviceAuthService
{
    // zh-CN: 这里复用 VS Code Copilot 扩展公开的 Client ID，以兼容 GitHub 的设备码流程。
    // en: Reuse the public VS Code Copilot client id so the GitHub device-code flow matches the expected app registration.
    private const string ClientId = "Iv1.b507a08c87ecfe98";
    private const string DeviceCodeUrl = "https://github.com/login/device/code";
    private const string AccessTokenUrl = "https://github.com/login/oauth/access_token";

    private readonly HttpClient _httpClient;
    private readonly ProviderAccountService _accountService;
    private readonly EncryptionService _encryption;
    private readonly CopilotTokenService _copilotTokenService;

    // zh-CN: 设备码流程跨轮询请求共享状态，因此用静态并发字典跟踪当前授权会话。
    // en: Device-code flows share state across poll requests, so a static concurrent dictionary tracks the active authorization sessions.
    private static readonly ConcurrentDictionary<Guid, DeviceCodeSession> _sessions = new();

    /// <summary>
    /// Initializes the typed HTTP client service that drives GitHub device authorization, persists OAuth tokens, and coordinates Copilot token cache invalidation.
    /// 初始化基于类型化 HttpClient 的 GitHub 设备授权服务，用于持久化 OAuth 令牌并协调 Copilot 令牌缓存失效。
    /// </summary>
    /// <param name="httpClient">HTTP client used for GitHub device-code and access-token endpoints. / 用于访问 GitHub 设备码与访问令牌端点的 HTTP 客户端。</param>
    /// <param name="accountService">Provider-account service used to store the acquired OAuth token. / 用于保存获取到的 OAuth 令牌的提供商账户服务。</param>
    /// <param name="encryption">Encryption service retained alongside protected provider configuration handling. / 与受保护提供商配置处理一起保留的加密服务。</param>
    /// <param name="copilotTokenService">Shared Copilot token cache cleared after device reauthorization. / 设备重新授权后需要清空的共享 Copilot 令牌缓存服务。</param>
    public GitHubDeviceAuthService(
        HttpClient httpClient,
        ProviderAccountService accountService,
        EncryptionService encryption,
        CopilotTokenService copilotTokenService)
    {
        _httpClient = httpClient;
        _accountService = accountService;
        _encryption = encryption;
        _copilotTokenService = copilotTokenService;
    }

    /// <summary>
    /// 发起设备码授权流程。
    /// Starts the device-code authorization flow.
    /// </summary>
    public async Task<DeviceCodeResponse> InitiateAsync(Guid providerId, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, DeviceCodeUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["scope"] = "read:user"
            })
        };
        request.Headers.Accept.Add(new("application/json"));

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<GitHubDeviceCodeResponse>(json)
            ?? throw new InvalidOperationException("Invalid response from GitHub");

        var session = new DeviceCodeSession
        {
            DeviceCode = result.DeviceCode,
            UserCode = result.UserCode,
            VerificationUri = result.VerificationUri,
            ExpiresAt = DateTime.UtcNow.AddSeconds(result.ExpiresIn),
            Interval = result.Interval
        };

        _sessions[providerId] = session;

        return new DeviceCodeResponse
        {
            UserCode = result.UserCode,
            VerificationUri = result.VerificationUri,
            ExpiresIn = result.ExpiresIn,
            Interval = result.Interval
        };
    }

    /// <summary>
    /// 轮询检查授权状态。
    /// Polls the current authorization status.
    /// </summary>
    public async Task<PollResult> PollAsync(Guid providerId, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(providerId, out var session))
        {
            return new PollResult { Status = "no_session", Message = "没有进行中的授权流程" };
        }

        if (DateTime.UtcNow > session.ExpiresAt)
        {
            _sessions.TryRemove(providerId, out _);
            return new PollResult { Status = "expired", Message = "授权已过期，请重新发起" };
        }

        var request = new HttpRequestMessage(HttpMethod.Post, AccessTokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["device_code"] = session.DeviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            })
        };
        request.Headers.Accept.Add(new("application/json"));

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<GitHubTokenResponse>(json);

        if (result == null)
        {
            return new PollResult { Status = "error", Message = "无法解析 GitHub 响应" };
        }

        // 授权成功
        if (!string.IsNullOrEmpty(result.AccessToken))
        {
            _sessions.TryRemove(providerId, out _);

            // zh-CN: 设备码流程拿到的是 GitHub OAuth token，后续由 Copilot token 服务再交换为 github_token。
            // en: The device-code flow yields a GitHub OAuth token, which the Copilot token service later exchanges for github_token.
            await _accountService.UpdateEnvConfigFieldAsync(providerId, "OAuthToken", result.AccessToken, ct);

            // zh-CN: 旧缓存中的 github_token 可能已经失效，因此这里主动清空。
            // en: Cached github_token values may now be stale, so clear them proactively.
            _copilotTokenService.ClearCache();

            return new PollResult { Status = "success", Message = "授权成功" };
        }

        // 处理错误状态
        return result.Error switch
        {
            "authorization_pending" => new PollResult
            {
                Status = "pending",
                Message = "等待用户授权...",
                Interval = session.Interval
            },
            "slow_down" => new PollResult
            {
                Status = "pending",
                Message = "等待用户授权...",
                Interval = session.Interval + 5
            },
            "expired_token" => new PollResult
            {
                Status = "expired",
                Message = "授权已过期，请重新发起"
            },
            "access_denied" => new PollResult
            {
                Status = "denied",
                Message = "用户拒绝了授权"
            },
            _ => new PollResult
            {
                Status = "error",
                Message = result.ErrorDescription ?? $"未知错误: {result.Error}"
            }
        };
    }

    /// <summary>
    /// 取消进行中的授权。
    /// Cancels an in-progress authorization flow.
    /// </summary>
    public bool Cancel(Guid providerId)
    {
        return _sessions.TryRemove(providerId, out _);
    }
}

#region DTOs

/// <summary>
/// 设备码授权启动结果。
/// Result returned when the device-code flow starts.
/// </summary>
public class DeviceCodeResponse
{
    /// <summary>用户验证码。 / User verification code.</summary>
    public string UserCode { get; set; } = string.Empty;

    /// <summary>验证地址。 / Verification URL.</summary>
    public string VerificationUri { get; set; } = string.Empty;

    /// <summary>剩余有效秒数。 / Remaining lifetime in seconds.</summary>
    public int ExpiresIn { get; set; }

    /// <summary>建议轮询间隔秒数。 / Recommended polling interval in seconds.</summary>
    public int Interval { get; set; }
}

/// <summary>
/// 设备码授权轮询结果。
/// Polling result for the device-code flow.
/// </summary>
public class PollResult
{
    /// <summary>授权状态，例如 success、pending 或 denied。 / Authorization state such as success, pending, or denied.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>状态说明。 / Status message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>可选的轮询间隔覆盖值。 / Optional polling interval override.</summary>
    public int? Interval { get; set; }
}

internal class DeviceCodeSession
{
    public string DeviceCode { get; set; } = string.Empty;
    public string UserCode { get; set; } = string.Empty;
    public string VerificationUri { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int Interval { get; set; }
}

internal class GitHubDeviceCodeResponse
{
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; set; } = string.Empty;

    [JsonPropertyName("user_code")]
    public string UserCode { get; set; } = string.Empty;

    [JsonPropertyName("verification_uri")]
    public string VerificationUri { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("interval")]
    public int Interval { get; set; }
}

internal class GitHubTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}

#endregion


