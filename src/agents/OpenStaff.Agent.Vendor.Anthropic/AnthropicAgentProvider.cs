using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using AgentResponse = OpenStaff.Core.Agents.AgentResponse;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

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

    public IAgent CreateAgent(AgentRole role)
    {
        var config = AgentConfig.FromJson(role.Config);
        var apiKey = config.GetRequired("apiKey");
        var model = config.Get("model") ?? "claude-sonnet-4-20250514";

        var client = new AnthropicClient { ApiKey = apiKey };
        IChatClient chatClient = client.AsIChatClient(model);

        var systemPrompt = role.SystemPrompt ?? "";
        var logger = _loggerFactory.CreateLogger<AnthropicAgent>();

        return new AnthropicAgent(role.RoleType, chatClient, systemPrompt, role.Name, _loggerFactory, logger);
    }
}

public class AnthropicAgent : AgentBase
{
    private readonly string _roleType;
    private readonly IChatClient _chatClient;
    private readonly string _systemPrompt;
    private readonly string _agentName;
    private readonly ILoggerFactory _loggerFactory;

    public AnthropicAgent(
        string roleType,
        IChatClient chatClient,
        string systemPrompt,
        string agentName,
        ILoggerFactory loggerFactory,
        ILogger<AnthropicAgent> logger) : base(logger)
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
                    ["provider"] = "anthropic"
                }
            };
        }
        catch (Exception ex)
        {
            Status = AgentStatus.Error;
            Logger.LogError(ex, "Anthropic agent processing failed");
            return new AgentResponse
            {
                Success = false,
                Content = $"处理失败: {ex.Message}",
                Errors = [ex.Message]
            };
        }
    }
}
