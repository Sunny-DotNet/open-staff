using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace OpenStaff.Agent.Vendor.GitHubCopilot;

public class GitHubCopilotAgentProvider : IAgentProvider
{
    private readonly ILoggerFactory _loggerFactory;

    public GitHubCopilotAgentProvider(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public string ProviderType => "github-copilot";
    public string DisplayName => "GitHub Copilot";

    public string? AvatarDataUri => "https://unpkg.com/@lobehub/icons-static-png@latest/light/githubcopilot.png";

    public AgentConfigSchema GetConfigSchema() => new()
    {
        ProviderType = ProviderType,
        DisplayName = DisplayName,
        Description = "GitHub Copilot 智能体（需要 Copilot 订阅，Token 自动管理）",
        Fields =
        [
            new AgentConfigField
            {
                Key = "token",
                Label = "Copilot Token",
                FieldType = "password",
                Required = true,
                Placeholder = "自动获取或手动填入"
            },
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
                    new() { Label = "Claude Sonnet 4", Value = "claude-sonnet-4-20250514" },
                    new() { Label = "Gemini 2.5 Flash", Value = "gemini-2.5-flash" }
                ]
            }
        ]
    };

    public AIAgent CreateAgent(AgentRole role, ResolvedProvider provider)
    {
        var config = AgentConfig.FromJson(role.Config);
        var token = config.GetRequired("token");
        var model = config.Get("model") ?? "gpt-4o";
        var systemPrompt = role.SystemPrompt ?? "";

        var endpoint = "https://api.individual.githubcopilot.com";
        var credential = new ApiKeyCredential(token);
        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        options.AddPolicy(new CopilotHeaderPolicy(), PipelinePosition.PerCall);

        var client = new OpenAIClient(credential, options);
        IChatClient chatClient = client.GetChatClient(model).AsIChatClient();

        return new ChatClientAgent(
            chatClient,
            name: role.Name,
            instructions: systemPrompt,
            loggerFactory: _loggerFactory);
    }

    private sealed class CopilotHeaderPolicy : PipelinePolicy
    {
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            AddHeaders(message);
            ProcessNext(message, pipeline, currentIndex);
        }

        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            AddHeaders(message);
            await ProcessNextAsync(message, pipeline, currentIndex);
        }

        private static void AddHeaders(PipelineMessage message)
        {
            var headers = message.Request.Headers;
            headers.Set("Editor-Version", "vscode/1.96.2");
            headers.Set("X-Github-Api-Version", "2025-04-01");
            headers.Set("User-Agent", "GitHubCopilotChat/1.0.102");
        }
    }
}
