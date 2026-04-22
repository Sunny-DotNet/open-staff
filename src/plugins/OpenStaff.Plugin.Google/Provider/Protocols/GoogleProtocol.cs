using OpenStaff.Provider.Models;

namespace OpenStaff.Provider.Protocols;

/// <summary>
/// Google 供应商协议，使用共享目录公开 Gemini Generate Content 兼容模型。
/// Google vendor protocol that exposes Gemini Generate Content compatible models from the shared catalog.
/// </summary>
internal class GoogleProtocol(IServiceProvider serviceProvider) : VendorProtocolBase<GoogleProtocolEnv>(serviceProvider)
{
    public override string ProtocolName => "Google";

    public override string ProtocolKey => "google";

    public override string Logo => "Google.Color";

    public override ModelProtocolType ProtocolType => ModelProtocolType.GoogleGenerateContent;
}

/// <summary>
/// Google 协议环境配置。
/// Environment settings for the Google protocol.
/// </summary>
public class GoogleProtocolEnv : ProtocolApiKeyEnvironmentBase
{
    public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta2";

    public override string? BaseUrl { get; set; }

    protected override string ApiKeyFromEnvDefault => "GOOGLE_API_KEY";
}
