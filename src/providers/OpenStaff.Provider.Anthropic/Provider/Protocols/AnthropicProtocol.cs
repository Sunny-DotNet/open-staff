using OpenStaff.Provider.Models;

namespace OpenStaff.Provider.Protocols;

internal class AnthropicProtocol(IServiceProvider serviceProvider) : VendorProtocolBase<AnthropicProtocolEnv>(serviceProvider)
{
    public override string ProviderKey => "anthropic";

    public override string Logo => "Claude.Color";
    public override string ProviderName => "Anthropic";

    public override ModelProtocolType ProtocolType => ModelProtocolType.AnthropicMessages;
}


public class AnthropicProtocolEnv:ProtocolEnvBase
{
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
    public bool FromEnv { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
    public string EnvName { get; set; } = string.Empty;
}