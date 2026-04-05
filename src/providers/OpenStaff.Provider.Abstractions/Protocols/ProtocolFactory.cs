using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenStaff.Provider.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Provider.Protocols;

public interface IProtocolFactory
{
    IEnumerable<IProtocol> AllProtocols();
    TProtocol CreateProtocol<TProtocol>() where TProtocol : IProtocol;
}

internal class ProtocolFactory : IProtocolFactory
{
    protected IServiceProvider ServiceProvider { get; }
    protected IOptions<ProviderOptions> ProviderOptions { get; }

    public ProtocolFactory(IServiceProvider serviceProvider, IOptions<ProviderOptions> providerOptions)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ServiceProvider = serviceProvider;
        ProviderOptions = providerOptions;
    }
    public virtual TProtocol CreateProtocol<TProtocol>() where TProtocol : IProtocol
    {
        return ActivatorUtilities.CreateInstance<TProtocol>(ServiceProvider);
    }
    public virtual IEnumerable<IProtocol> AllProtocols()
    {
        var protocols = new List<IProtocol>();
        foreach (var protocolType in ProviderOptions.Value.Protocols)
        {
            if (!typeof(IProtocol).IsAssignableFrom(protocolType))
            {
                throw new InvalidOperationException($"Type {protocolType.FullName} does not implement IProtocol.");
            }
            var protocol = (IProtocol)ActivatorUtilities.CreateInstance(ServiceProvider, protocolType);
            protocols.Add(protocol);
        }
        return protocols;
    }
}
