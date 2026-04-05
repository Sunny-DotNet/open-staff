using OpenStaff.Provider.Protocols;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Provider.Options;

public class ProviderOptions
{
    private readonly List<Type> _protocols = new();
    public IReadOnlyCollection<Type> Protocols => _protocols.AsReadOnly();

    public void AddProtocol<TProtocol>() where TProtocol : IProtocol
    {
        if (_protocols.Contains(typeof(TProtocol))) return;
        _protocols.Add(typeof(TProtocol));
    }
}
