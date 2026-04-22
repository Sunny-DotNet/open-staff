using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Platform;

public sealed class OpenAIPlatform(IServiceProvider serviceProvider)
    : IPlatform, IHasProtocol, IHasChatClientFactory
{

    public string PlatformKey => "openai";

    public IChatClientFactory GetChatClientFactory() => ActivatorUtilities.CreateInstance<OpenAIChatClientFactory>(serviceProvider);
    public IProtocol GetProtocol() => ActivatorUtilities.CreateInstance<OpenAIProtocol>(serviceProvider);
}
