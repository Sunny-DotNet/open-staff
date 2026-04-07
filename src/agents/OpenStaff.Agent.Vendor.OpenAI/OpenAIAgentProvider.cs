using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using System.ClientModel;

namespace OpenStaff.Agent.Vendor.OpenAI;

public class OpenAIAgentProvider : IAgentProvider
{
    private readonly ILoggerFactory _loggerFactory;

    public OpenAIAgentProvider(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public string ProviderType => "openai";
    public string DisplayName => "OpenAI";

    public string? AvatarDataUri => "https://unpkg.com/@lobehub/icons-static-png@latest/light/openai.png";

    public AgentConfigSchema GetConfigSchema() => new()
    {
        ProviderType = ProviderType,
        DisplayName = DisplayName,
        Description = "OpenAI GPT 系列模型",
        Fields =
        [
            new AgentConfigField
            {
                Key = "model",
                Label = "模型",
                FieldType = "select",
                Required = true,
                DefaultValue = "gpt-4o",
                Options =
                [
                    new() { Label = "GPT-4o", Value = "gpt-4o" },
                    new() { Label = "GPT-4o Mini", Value = "gpt-4o-mini" },
                    new() { Label = "GPT-4.1", Value = "gpt-4.1" },
                    new() { Label = "GPT-4.1 Mini", Value = "gpt-4.1-mini" },
                    new() { Label = "o3-mini", Value = "o3-mini" }
                ]
            }
        ]
    };

    public AIAgent CreateAgent(AgentRole role, ResolvedProvider provider)
    {
        var apiKey = provider.ApiKey
            ?? throw new InvalidOperationException("请先在供应商管理中配置 OpenAI API Key");

        var config = AgentConfig.FromJson(role.Config);
        var model = config.Get("model") ?? "gpt-4o";
        var systemPrompt = role.SystemPrompt ?? "";

        var credential = new ApiKeyCredential(apiKey);
        var options = new OpenAIClientOptions();
        if (!string.IsNullOrEmpty(provider.BaseUrl))
            options.Endpoint = new Uri(provider.BaseUrl);

        var client = new OpenAIClient(credential, options);
        IChatClient chatClient = client.GetChatClient(model).AsIChatClient();

        return new ChatClientAgent(
            chatClient,
            name: role.Name,
            instructions: systemPrompt,
            loggerFactory: _loggerFactory);
    }
}
