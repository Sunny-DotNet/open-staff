using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Provider.Protocols;

internal class ProtocolFactory
{
    protected IServiceProvider ServiceProvider { get; }
    public ProtocolFactory(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ServiceProvider = serviceProvider;
    }
    public virtual TProtocol CreateProtocol<TProtocol>()where TProtocol : IProtocol
    {
        return ActivatorUtilities.CreateInstance<TProtocol>(ServiceProvider);
    }
}
