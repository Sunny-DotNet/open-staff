using OpenStaff.Provider.Models;

namespace OpenStaff.Provider.Protocols;

/// <summary>
/// GitHub Copilot 协议 — 通过 device-auth 获取 OAuth Token，再换取 Copilot API Token
/// 非供应商协议，模型列表通过 Copilot API 动态获取
/// </summary>
internal class GitHubCopilotProtocol(IServiceProvider serviceProvider) : ProtocolBase<GitHubCopilotProtocolEnv>(serviceProvider)
{
    public override bool IsVendor => false;

    public override string ProviderName => "Github Copilot";

    public override string ProviderKey => "github-copilot";

    public override string Logo => "GithubCopilot";

    public override async Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
    {
        // GitHub Copilot 模型列表通过 /v1/models 端点动态获取
        // 需要先通过 CopilotTokenService 将 OAuthToken 换取为 github_token
        await Task.CompletedTask;
        return [];
    }
}

public class GitHubCopilotProtocolEnv : ProtocolEnvBase
{
    public string OAuthToken { get; set; } = string.Empty;
}
