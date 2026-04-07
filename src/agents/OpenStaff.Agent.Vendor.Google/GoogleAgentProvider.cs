using Google.GenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using AgentResponse = OpenStaff.Core.Agents.AgentResponse;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

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

    public IAgent CreateAgent(AgentRole role)
    {
        var config = AgentConfig.FromJson(role.Config);
        var apiKey = config.GetRequired("apiKey");
        var model = config.Get("model") ?? "gemini-2.5-flash";

        var client = new Client(vertexAI: false, apiKey: apiKey);
        IChatClient chatClient = client.AsIChatClient(model);

        var systemPrompt = role.SystemPrompt ?? "";
        var logger = _loggerFactory.CreateLogger<GoogleAgent>();

        return new GoogleAgent(role.RoleType, chatClient, systemPrompt, role.Name, _loggerFactory, logger);
    }
}

public class GoogleAgent : AgentBase
{
    private readonly string _roleType;
    private readonly IChatClient _chatClient;
    private readonly string _systemPrompt;
    private readonly string _agentName;
    private readonly ILoggerFactory _loggerFactory;

    public GoogleAgent(
        string roleType,
        IChatClient chatClient,
        string systemPrompt,
        string agentName,
        ILoggerFactory loggerFactory,
        ILogger<GoogleAgent> logger) : base(logger)
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
                    ["provider"] = "google"
                }
            };
        }
        catch (Exception ex)
        {
            Status = AgentStatus.Error;
            Logger.LogError(ex, "Google agent processing failed");
            return new AgentResponse
            {
                Success = false,
                Content = $"处理失败: {ex.Message}",
                Errors = [ex.Message]
            };
        }
    }
}
