using OpenStaff.Provider.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Provider.Protocols;

internal class OpenAIProtocol(IServiceProvider serviceProvider) : VendorProtocolBase<OpenAIProtocolEnv>(serviceProvider)
{
    public override string ProviderKey => "openai";

    public override string Logo => "OpenAI";
    public override string ProviderName => "OpenAI";

    public override ModelProtocolType ProtocolType => ModelProtocolType.OpenAIChatCompletions | ModelProtocolType.OpenAIResponse;

}
public class OpenAIProtocolEnv: ProtocolHasApiKeyEnvBase
{
    public override string BaseUrl { get; set; } = "https://api.openai.com/v1";

    protected override string ApiKeyFromEnvDefault => "OPENAI_API_KEY";
}