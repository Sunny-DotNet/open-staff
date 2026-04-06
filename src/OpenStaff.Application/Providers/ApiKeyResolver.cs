using System.Text.Json;
using OpenStaff.Application.Auth;
using OpenStaff.Core.Models;

namespace OpenStaff.Application.Providers;

/// <summary>
/// API Key 解析服务 — 从 ProviderAccount 的 EnvConfig 提取 API Key
/// GitHub Copilot: oauth_token → github_token 交换
/// 其他供应商: 根据 FromEnv 决定读环境变量还是直接使用
/// </summary>
public class ApiKeyResolver
{
    private readonly ProviderAccountService _accountService;
    private readonly CopilotTokenService _copilotTokenService;

    public ApiKeyResolver(ProviderAccountService accountService, CopilotTokenService copilotTokenService)
    {
        _accountService = accountService;
        _copilotTokenService = copilotTokenService;
    }

    public async Task<ResolvedApiKey> ResolveAsync(ProviderAccount account, CancellationToken ct = default)
    {
        var envDict = _accountService.GetEnvConfigDict(account);
        if (envDict == null)
            return new ResolvedApiKey { ApiKey = null };

        // GitHub Copilot 特殊处理
        if (account.ProtocolType == "github-copilot")
        {
            var oauthToken = GetStringValue(envDict, "OAuthToken");
            if (string.IsNullOrEmpty(oauthToken))
                return new ResolvedApiKey { ApiKey = null };

            var copilotToken = await _copilotTokenService.GetTokenAsync(oauthToken, ct);
            return new ResolvedApiKey
            {
                ApiKey = copilotToken.Token,
                EndpointOverride = copilotToken.ChatCompletionsEndpoint
            };
        }

        // 标准协议：根据 FromEnv 决定 API Key 来源
        var fromEnv = GetBoolValue(envDict, "FromEnv");
        string? apiKey;

        if (fromEnv)
        {
            var envName = GetStringValue(envDict, "EnvName");
            apiKey = !string.IsNullOrEmpty(envName) ? Environment.GetEnvironmentVariable(envName) : null;
        }
        else
        {
            apiKey = GetStringValue(envDict, "ApiKey");
        }

        var baseUrl = GetStringValue(envDict, "BaseUrl");

        return new ResolvedApiKey { ApiKey = apiKey, BaseUrl = baseUrl };
    }

    private static string? GetStringValue(Dictionary<string, object?> dict, string key)
    {
        if (dict.TryGetValue(key, out var val) && val != null)
        {
            if (val is JsonElement je) return je.GetString();
            return val.ToString();
        }
        return null;
    }

    private static bool GetBoolValue(Dictionary<string, object?> dict, string key)
    {
        if (dict.TryGetValue(key, out var val) && val != null)
        {
            if (val is JsonElement je) return je.ValueKind == JsonValueKind.True;
            if (val is bool b) return b;
        }
        return false;
    }
}

/// <summary>
/// 解析后的 API Key 结果
/// </summary>
public class ResolvedApiKey
{
    public string? ApiKey { get; set; }
    public string? EndpointOverride { get; set; }
    public string? BaseUrl { get; set; }
}
