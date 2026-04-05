using OpenStaff.Core.Modularity;
using OpenStaff.Provider.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Provider;

[DependsOn(typeof(ProviderAbstractionsModule))]
public class OpenStaffProviderOpenAIModule:OpenStaffModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<ProviderOptions>(options => { 
            options.AddProtocol<Protocols.OpenAIProtocol>();
        });
    }
}
