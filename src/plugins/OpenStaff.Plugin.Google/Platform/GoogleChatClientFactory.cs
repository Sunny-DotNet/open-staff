using OpenStaff.Provider.Platforms;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Platform;

internal class GoogleChatClientFactory : DefaultChatClientFactoryBase
{
    public GoogleChatClientFactory(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }
}
