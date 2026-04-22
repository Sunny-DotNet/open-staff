using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent;
using OpenStaff.Plugin.NewApi;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Platform;

public sealed class NewApiPlatform(
    IServiceProvider serviceProvider,
    NewApiPlatformMetadataService vendorMetadataService)
    : IPlatform, IHasProtocol, IHasChatClientFactory, IHasVendorMetadata
{
    public string PlatformKey => "newapi";

    public IProtocol GetProtocol() => ActivatorUtilities.CreateInstance<NewApiProtocol>(serviceProvider);

    public IChatClientFactory GetChatClientFactory() => ActivatorUtilities.CreateInstance<NewApiChatClientFactory>(serviceProvider);

    public IVendorPlatformMetadata GetVendorMetadataService() => vendorMetadataService;
}
