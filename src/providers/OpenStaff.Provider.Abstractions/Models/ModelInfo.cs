using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Provider.Models;

public record class ModelInfo(string ModelSlug, string VenderSlug, ModelProtocolType ModelProtocols);
public enum ModelProtocolType:short
{
    OpenAIChatCompletions=1<<1,
    OpenAIResponse=1<<2,
    GoogleGenerateContent=1<<3,
    AnthropicMessages=1<<4
}