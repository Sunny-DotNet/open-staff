using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;

namespace OpenStaff.Agent.Vendor.Anthropic;

public class AnthropicAgentProvider : IAgentProvider
{
    private readonly ILoggerFactory _loggerFactory;

    public AnthropicAgentProvider(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public string ProviderType => "anthropic";
    public string DisplayName => "Anthropic Claude";

    public AgentConfigSchema GetConfigSchema() => new()
    {
        ProviderType = ProviderType,
        DisplayName = DisplayName,
        Description = "Anthropic Claude 系列模型（Claude Sonnet、Opus、Haiku 等）",
        Fields =
        [
            new AgentConfigField
            {
                Key = "apiKey",
                Label = "API Key",
                FieldType = "password",
                Required = true,
                Placeholder = "sk-ant-..."
            },
            new AgentConfigField
            {
                Key = "model",
                Label = "模型",
                FieldType = "select",
                Required = true,
                DefaultValue = "claude-sonnet-4-20250514",
                Options =
                [
                    new() { Label = "Claude Sonnet 4", Value = "claude-sonnet-4-20250514" },
                    new() { Label = "Claude Opus 4", Value = "claude-opus-4-20250514" },
                    new() { Label = "Claude Haiku 3.5", Value = "claude-3-5-haiku-20241022" }
                ]
            }
        ]
    };

    public AIAgent CreateAgent(AgentRole role, ResolvedProvider provider)
    {
        var config = AgentConfig.FromJson(role.Config);
        var apiKey = config.GetRequired("apiKey");
        var model = config.Get("model") ?? "claude-sonnet-4-20250514";
        var systemPrompt = role.SystemPrompt ?? "";

        var client = new AnthropicClient { ApiKey = apiKey };
        return client.AsAIAgent(
            model: model,
            name: role.Name,
            instructions: systemPrompt);
    }
}
