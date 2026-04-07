using Google.GenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;

namespace OpenStaff.Agent.Vendor.Google;

public class GoogleAgentProvider : IAgentProvider
{
    private readonly ILoggerFactory _loggerFactory;

    public GoogleAgentProvider(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public string ProviderType => "google";
    public string DisplayName => "Google Gemini";

    public string? AvatarDataUri => "data:image/svg+xml;base64,"
        + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128"><rect width="128" height="128" rx="24" fill="#1A73E8"/><text x="64" y="82" text-anchor="middle" font-size="64" font-family="sans-serif" fill="white">G</text></svg>"""));

    public AgentConfigSchema GetConfigSchema() => new()
    {
        ProviderType = ProviderType,
        DisplayName = DisplayName,
        Description = "Google Gemini 系列模型",
        Fields =
        [
            new AgentConfigField
            {
                Key = "apiKey",
                Label = "API Key",
                FieldType = "password",
                Required = true,
                Placeholder = "AIza..."
            },
            new AgentConfigField
            {
                Key = "model",
                Label = "模型",
                FieldType = "select",
                Required = true,
                DefaultValue = "gemini-2.5-flash",
                Options =
                [
                    new() { Label = "Gemini 2.5 Flash", Value = "gemini-2.5-flash" },
                    new() { Label = "Gemini 2.5 Pro", Value = "gemini-2.5-pro" },
                    new() { Label = "Gemini 2.0 Flash", Value = "gemini-2.0-flash" }
                ]
            }
        ]
    };

    public AIAgent CreateAgent(AgentRole role, ResolvedProvider provider)
    {
        var config = AgentConfig.FromJson(role.Config);
        var apiKey = config.GetRequired("apiKey");
        var model = config.Get("model") ?? "gemini-2.5-flash";
        var systemPrompt = role.SystemPrompt ?? "";

        var client = new Client(vertexAI: false, apiKey: apiKey);
        IChatClient chatClient = client.AsIChatClient(model);

        return new ChatClientAgent(
            chatClient,
            name: role.Name,
            instructions: systemPrompt,
            loggerFactory: _loggerFactory);
    }
}
