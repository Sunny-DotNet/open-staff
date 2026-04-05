using OpenStaff.Provider.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Provider.Protocols;

internal class OpenAIProtocol(IServiceProvider serviceProvider) : VendorProtocolBase<OpenAIProtocolEnv>(serviceProvider)
{
    public override string ProviderName => "openai";

    public override ModelProtocolType ProtocolType => ModelProtocolType.OpenAIChatCompletions | ModelProtocolType.OpenAIResponse;
}
public class OpenAIProtocolEnv: ProtocolEnvBase
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public bool FromEnv { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
    public string EnvName { get; set; } = string.Empty;
}