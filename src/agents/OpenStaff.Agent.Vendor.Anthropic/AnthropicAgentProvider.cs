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

    public string? AvatarDataUri => "data:image/svg+xml;base64,"
        + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128"><rect width="128" height="128" rx="24" fill="#D4A574"/><text x="64" y="82" text-anchor="middle" font-size="64" font-family="sans-serif" fill="white">A</text></svg>"""));

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
