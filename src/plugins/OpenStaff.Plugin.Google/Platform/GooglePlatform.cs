using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent;
using OpenStaff.Plugin.Google;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Platform;

public sealed class GooglePlatform(
    IServiceProvider serviceProvider)
    : IPlatform, IHasProtocol, IHasChatClientFactory
{

    public string PlatformKey => "google";

    public IProtocol GetProtocol() => ActivatorUtilities.CreateInstance<GoogleProtocol>(serviceProvider);

    public IChatClientFactory GetChatClientFactory() =>  ActivatorUtilities.CreateInstance<GoogleChatClientFactory>(serviceProvider);
}
