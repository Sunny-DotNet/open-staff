using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

using OpenStaff.Application.Providers;
using OpenStaff.Infrastructure.Security;

namespace OpenStaff.Application.Auth;

/// <summary>
/// GitHub 设备码授权服务 / GitHub Device Code Authorization Service
/// </summary>
public class GitHubDeviceAuthService
{
    // VS Code Copilot 扩展的公开 Client ID
    private const string ClientId = "Iv1.b507a08c87ecfe98";
    private const string DeviceCodeUrl = "https://github.com/login/device/code";
    private const string AccessTokenUrl = "https://github.com/login/oauth/access_token";

    private readonly HttpClient _httpClient;
    private readonly ProviderAccountService _accountService;
    private readonly EncryptionService _encryption;
    private readonly CopilotTokenService _copilotTokenService;

    // 存储进行中的设备码流程（accountId -> DeviceCodeSession）
    private static readonly ConcurrentDictionary<Guid, DeviceCodeSession> _sessions = new();

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
    /// 发起设备码授权流程
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
    /// 轮询检查授权状态
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

            // 将 oauth_token 存储到 ProviderAccount 的 EnvConfig
            await _accountService.UpdateEnvConfigFieldAsync(providerId, "OAuthToken", result.AccessToken);

            // 清除 Copilot token 缓存（旧 github_token 可能已失效）
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
    /// 取消进行中的授权
    /// </summary>
    public bool Cancel(Guid providerId)
    {
        return _sessions.TryRemove(providerId, out _);
    }
}

#region DTOs

public class DeviceCodeResponse
{
    public string UserCode { get; set; } = string.Empty;
    public string VerificationUri { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public int Interval { get; set; }
}

public class PollResult
{
    public string Status { get; set; } = string.Empty; // success, pending, expired, denied, error, no_session
    public string Message { get; set; } = string.Empty;
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
