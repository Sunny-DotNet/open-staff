using OpenStaff.Provider.Models;

namespace OpenStaff.Provider.Protocols;

internal class GoogleProtocol(IServiceProvider serviceProvider) : VendorProtocolBase(serviceProvider)
{
    public override string ProviderName => "google";

    public override ModelProtocolType ProtocolType => ModelProtocolType.GoogleGenerateContent;
}
