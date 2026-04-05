using OpenStaff.Provider.Models;

namespace OpenStaff.Provider.Protocols;

internal class AnthropicProtocol(IServiceProvider serviceProvider) : VendorProtocolBase(serviceProvider)
{
    public override string ProviderName => "anthropic";

    public override ModelProtocolType ProtocolType => ModelProtocolType.AnthropicMessages;
}
