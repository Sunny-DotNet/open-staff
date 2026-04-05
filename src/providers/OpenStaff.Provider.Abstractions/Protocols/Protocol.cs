using OpenStaff.Provider.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Provider.Protocols;

public interface IProtocol { 
    string ProviderName { get; }
    Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default);
}
public abstract class ProtocolBase : IProtocol
{
    public abstract string ProviderName { get; }

    public abstract Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default);
}
