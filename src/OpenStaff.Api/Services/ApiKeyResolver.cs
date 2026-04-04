using OpenStaff.Core.Models;

namespace OpenStaff.Api.Services;

/// <summary>
/// API Key 解析服务 — 统一处理各供应商的 API Key 获取逻辑
/// 对于 GitHub Copilot: oauth_token → github_token 交换
/// 对于其他供应商: 直接解密或读取环境变量
/// </summary>
public class ApiKeyResolver
{
    private readonly FileProviderService _providerService;
    private readonly CopilotTokenService _copilotTokenService;

    public ApiKeyResolver(FileProviderService providerService, CopilotTokenService copilotTokenService)
    {
        _providerService = providerService;
        _copilotTokenService = copilotTokenService;
    }

    /// <summary>
    /// 解析可直接使用的 API Key
    /// Copilot: 自动将 oauth_token 交换为 github_token
    /// 其他供应商: 直接返回解密后的 key
    /// </summary>
    /// <returns>可直接用于 API 调用的 token; Copilot 还返回动态 endpoint</returns>
    public async Task<ResolvedApiKey> ResolveAsync(ModelProvider provider, CancellationToken ct = default)
    {
        // 先获取存储的原始 key（对 Copilot 来说是 oauth_token）
        var rawKey = _providerService.ResolveApiKey(provider);

        if (string.IsNullOrEmpty(rawKey))
        {
            return new ResolvedApiKey { ApiKey = null };
        }

        // Copilot 需要额外的 token 交换
        if (provider.ProviderType == ProviderTypes.GitHubCopilot)
        {
            var copilotToken = await _copilotTokenService.GetTokenAsync(rawKey, ct);
            return new ResolvedApiKey
            {
                ApiKey = copilotToken.Token,
                EndpointOverride = copilotToken.ChatCompletionsEndpoint
            };
        }

        return new ResolvedApiKey { ApiKey = rawKey };
    }
}

/// <summary>
/// 解析后的 API Key 结果
/// </summary>
public class ResolvedApiKey
{
    /// <summary>可直接使用的 API Key / token</summary>
    public string? ApiKey { get; set; }

    /// <summary>动态端点覆盖（Copilot token 响应可能包含特定 endpoint）</summary>
    public string? EndpointOverride { get; set; }
}
