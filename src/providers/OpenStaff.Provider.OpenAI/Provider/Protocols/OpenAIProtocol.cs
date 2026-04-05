using OpenStaff.Provider.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Provider.Protocols;

internal class OpenAIProtocol : ProtocolBase
{
    public override string ProviderName => "open-ai";

    public override Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
