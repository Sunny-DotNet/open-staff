using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using System.ClientModel;
using System.ClientModel.Primitives;
using AgentResponse = OpenStaff.Core.Agents.AgentResponse;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

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

    public IAgent CreateAgent(AgentRole role)
    {
        var config = AgentConfig.FromJson(role.Config);
        var token = config.GetRequired("token");
        var model = config.Get("model") ?? "gpt-4o";

        var endpoint = "https://api.individual.githubcopilot.com";
        var credential = new ApiKeyCredential(token);
        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        options.AddPolicy(new CopilotHeaderPolicy(), PipelinePosition.PerCall);

        var client = new OpenAIClient(credential, options);
        IChatClient chatClient = client.GetChatClient(model).AsIChatClient();

        var systemPrompt = role.SystemPrompt ?? "";
        var logger = _loggerFactory.CreateLogger<GitHubCopilotAgent>();

        return new GitHubCopilotAgent(role.RoleType, chatClient, systemPrompt, role.Name, _loggerFactory, logger);
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

public class GitHubCopilotAgent : AgentBase
{
    private readonly string _roleType;
    private readonly IChatClient _chatClient;
    private readonly string _systemPrompt;
    private readonly string _agentName;
    private readonly ILoggerFactory _loggerFactory;

    public GitHubCopilotAgent(
        string roleType,
        IChatClient chatClient,
        string systemPrompt,
        string agentName,
        ILoggerFactory loggerFactory,
        ILogger<GitHubCopilotAgent> logger) : base(logger)
    {
        _roleType = roleType;
        _chatClient = chatClient;
        _systemPrompt = systemPrompt;
        _agentName = agentName;
        _loggerFactory = loggerFactory;
    }

    public override string RoleType => _roleType;

    public override async Task<AgentResponse> ProcessAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        Status = AgentStatus.Thinking;
        try
        {
            var aiAgent = new ChatClientAgent(
                _chatClient,
                name: _agentName,
                instructions: _systemPrompt,
                loggerFactory: _loggerFactory);

            var chatMessages = new List<ChatMessage>
            {
                new(ChatRole.User, message.Content ?? "")
            };

            var result = await aiAgent.RunAsync(chatMessages, session: null, options: null, cancellationToken: cancellationToken);
            var content = result?.ToString() ?? "";

            Status = AgentStatus.Idle;
            return new AgentResponse
            {
                Success = true,
                Content = content,
                Data = new Dictionary<string, object>
                {
                    ["roleType"] = RoleType,
                    ["provider"] = "github-copilot"
                }
            };
        }
        catch (Exception ex)
        {
            Status = AgentStatus.Error;
            Logger.LogError(ex, "GitHub Copilot agent processing failed");
            return new AgentResponse
            {
                Success = false,
                Content = $"处理失败: {ex.Message}",
                Errors = [ex.Message]
            };
        }
    }
}
