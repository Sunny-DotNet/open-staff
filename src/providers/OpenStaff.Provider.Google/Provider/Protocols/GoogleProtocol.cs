using OpenStaff.Provider.Models;

namespace OpenStaff.Provider.Protocols;

internal class GoogleProtocol(IServiceProvider serviceProvider) : VendorProtocolBase<GoogleProtocolEnv>(serviceProvider)
{
    public override string ProviderName => "google";

    public override string ProviderKey => "google";

    public override string Logo => "Google.Color";

    public override ModelProtocolType ProtocolType => ModelProtocolType.GoogleGenerateContent;
}
public class GoogleProtocolEnv: ProtocolHasApiKeyEnvBase
{
    public override string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta2";

    protected override string ApiKeyFromEnvDefault => "GOOGLE_API_KEY";
}
