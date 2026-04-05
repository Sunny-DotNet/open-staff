using OpenStaff.Provider.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Provider.Protocols;

internal class OpenAIProtocol(IServiceProvider serviceProvider) : VendorProtocolBase(serviceProvider)
{
    public override string ProviderName => "openai";

    public override ModelProtocolType ProtocolType => ModelProtocolType.OpenAIChatCompletions | ModelProtocolType.OpenAIResponse;
}
