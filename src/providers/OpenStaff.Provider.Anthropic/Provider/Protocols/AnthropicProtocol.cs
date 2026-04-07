using OpenStaff.Provider.Models;

namespace OpenStaff.Provider.Protocols;

internal class AnthropicProtocol(IServiceProvider serviceProvider) : VendorProtocolBase<AnthropicProtocolEnv>(serviceProvider)
{
    public override string ProviderKey => "anthropic";

    public override string Logo => "Claude.Color";
    public override string ProviderName => "Anthropic";

    public override ModelProtocolType ProtocolType => ModelProtocolType.AnthropicMessages;
}


public class AnthropicProtocolEnv: ProtocolHasApiKeyEnvBase
{
    public override string BaseUrl { get; set; } = "https://api.anthropic.com";

    protected override string ApiKeyFromEnvDefault => "ANTHROPIC_AUTH_TOKEN";
}