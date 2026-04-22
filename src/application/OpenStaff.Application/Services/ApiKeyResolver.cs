using System.Text.Json;
using OpenStaff.Application.Auth.Services;
using OpenStaff.Entities;

namespace OpenStaff.Application.Providers.Services;
/// <summary>
/// API Key 解析服务 — 从 ProviderAccount 的 EnvConfig 提取 API Key
/// GitHub Copilot: oauth_token → github_token 交换
/// 其他供应商: 根据 FromEnv 决定读环境变量还是直接使用
/// </summary>
public class ApiKeyResolver
{
    private readonly ProviderAccountService _accountService;
    private readonly CopilotTokenService _copilotTokenService;

    /// <summary>
    /// Initializes the scoped API-key resolver with provider configuration access and the shared Copilot token cache used for GitHub token exchange.
    /// 使用提供商配置访问能力和 GitHub 令牌交换所需的共享 Copilot 令牌缓存初始化 Scoped API Key 解析器。
    /// </summary>
    /// <param name="accountService">Provider-account service used to read decrypted env/config settings. / 用于读取已解密环境/配置设置的提供商账户服务。</param>
    /// <param name="copilotTokenService">Singleton Copilot token cache that exchanges OAuth tokens for short-lived API tokens. / 将 OAuth 令牌交换为短期 API 令牌的单例 Copilot 令牌缓存服务。</param>
    public ApiKeyResolver(ProviderAccountService accountService, CopilotTokenService copilotTokenService)
    {
        _accountService = accountService;
        _copilotTokenService = copilotTokenService;
    }

    /// <summary>
    /// Resolves the effective API key for a provider account by reading env/config settings, optionally loading from process environment variables, and exchanging GitHub Copilot OAuth tokens when required.
    /// 通过读取环境/配置设置、按需从进程环境变量加载，并在 GitHub Copilot 场景下交换 OAuth 令牌，解析提供商账户的实际 API Key。
    /// </summary>
    /// <param name="account">Provider account whose effective API settings should be resolved. / 需要解析实际 API 设置的提供商账户。</param>
    /// <param name="ct">Cancellation token for asynchronous token exchange. / 用于异步令牌交换的取消令牌。</param>
    /// <returns>Resolved API key, optional endpoint override, and base URL information. / 解析后的 API Key、可选端点覆盖值以及基础地址信息。</returns>
    public async Task<ResolvedApiKey> ResolveAsync(ProviderAccount account, CancellationToken ct = default)
    {
        var envDict = await _accountService.GetEnvConfigDictAsync(account, ct);
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

    /// <summary>
    /// Reads a string value from an env/config dictionary and handles both plain CLR values and <see cref="JsonElement"/> payloads produced by JSON deserialization.
    /// 从环境/配置字典读取字符串值，并兼容 JSON 反序列化产生的普通 CLR 值与 <see cref="JsonElement"/>。
    /// </summary>
    /// <param name="dict">Dictionary containing decrypted provider settings. / 包含已解密提供商设置的字典。</param>
    /// <param name="key">Setting key to inspect. / 要检查的设置键。</param>
    /// <returns>Resolved string value, or <see langword="null"/> when unavailable. / 解析出的字符串值；不可用时返回 <see langword="null"/>。</returns>
    private static string? GetStringValue(Dictionary<string, object?> dict, string key)
    {
        if (dict.TryGetValue(key, out var val) && val != null)
        {
            if (val is JsonElement je) return je.GetString();
            return val.ToString();
        }
        return null;
    }

    /// <summary>
    /// Reads a boolean flag from an env/config dictionary, accepting boxed booleans and JSON boolean values while defaulting to <see langword="false"/>.
    /// 从环境/配置字典读取布尔标记，兼容装箱布尔值与 JSON 布尔值，并在缺失时默认返回 <see langword="false"/>。
    /// </summary>
    /// <param name="dict">Dictionary containing decrypted provider settings. / 包含已解密提供商设置的字典。</param>
    /// <param name="key">Setting key to inspect. / 要检查的设置键。</param>
    /// <returns>Resolved boolean flag. / 解析出的布尔标记。</returns>
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


