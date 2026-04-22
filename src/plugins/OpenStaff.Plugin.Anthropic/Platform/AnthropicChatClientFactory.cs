using OpenStaff.Provider.Platforms;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Platform;

internal class AnthropicChatClientFactory : DefaultChatClientFactoryBase
{
    public AnthropicChatClientFactory(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }
}
