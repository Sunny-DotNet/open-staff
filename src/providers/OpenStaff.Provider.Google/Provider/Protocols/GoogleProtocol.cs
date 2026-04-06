using OpenStaff.Provider.Models;

namespace OpenStaff.Provider.Protocols;

internal class GoogleProtocol(IServiceProvider serviceProvider) : VendorProtocolBase<GoogleProtocolEnv>(serviceProvider)
{
    public override string ProviderName => "google";

    public override string ProviderKey => "google";

    public override string Logo => "Google.Color";

    public override ModelProtocolType ProtocolType => ModelProtocolType.GoogleGenerateContent;
}
public class GoogleProtocolEnv: ProtocolEnvBase
{
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta2";
    public bool FromEnv { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
    public string EnvName { get; set; } = string.Empty;
}
