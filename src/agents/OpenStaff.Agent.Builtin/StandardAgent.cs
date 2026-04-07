using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent.Tools;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using AgentResponse = OpenStaff.Core.Agents.AgentResponse;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace OpenStaff.Agent.Builtin;

/// <summary>
/// 标准智能体 — 所有内置/自定义角色使用同一实现
/// </summary>
public class StandardAgent : AgentBase
{
    private readonly RoleConfig _config;
    private readonly IAgentToolRegistry _toolRegistry;
    private readonly AIAgentFactory _aiAgentFactory;

    public StandardAgent(
        RoleConfig config,
        IAgentToolRegistry toolRegistry,
        AIAgentFactory aiAgentFactory,
        ILogger<StandardAgent> logger) : base(logger)
    {
        _config = config;
        _toolRegistry = toolRegistry;
        _aiAgentFactory = aiAgentFactory;
    }

    public override string RoleType => _config.RoleType;

    public override async Task<AgentResponse> ProcessAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        Status = AgentStatus.Thinking;
        try
        {
            var systemPrompt = _config.SystemPrompt;

            var preview = message.Content?.Length > 100
                ? message.Content.Substring(0, 100) + "..."
                : message.Content ?? "";
            await PublishEventAsync(EventTypes.Thought, $"[{_config.Name}] 正在处理: {preview}");

            if (Context?.Account == null || string.IsNullOrEmpty(Context.ApiKey))
            {
                return new AgentResponse
                {
                    Success = false,
                    Content = "模型供应商未配置 / Model provider not configured",
                    Errors = new List<string> { "No provider or API key in context" }
                };
            }

            IList<AITool>? aiTools = null;
            if (_config.Tools.Count > 0 && Context != null)
            {
                var agentTools = _toolRegistry.GetTools(_config.Tools);
                if (agentTools.Count > 0)
                {
                    aiTools = AgentToolBridge.ToAITools(agentTools, Context);
                    Logger.LogInformation("Agent {Role} loaded {Count} tools: {Names}",
                        _config.RoleType, aiTools.Count,
                        string.Join(", ", agentTools.Select(t => t.Name)));
                }
            }

            var modelName = _config.ModelName ?? Context!.Role?.ModelName ?? "gpt-4o";
            var aiAgent = _aiAgentFactory.CreateAgent(
                protocolType: Context!.Account!.ProtocolType,
                apiKey: Context!.ApiKey!,
                model: modelName,
                baseUrl: Context.ExtraConfig.TryGetValue("EndpointOverride", out var ep) ? ep?.ToString() : null,
                instructions: systemPrompt,
                agentName: _config.Name,
                tools: aiTools);

            var chatMessages = new List<ChatMessage>
            {
                new(ChatRole.User, message.Content ?? "")
            };

            var result = await aiAgent.RunAsync(chatMessages, session: null, options: null, cancellationToken: cancellationToken);
            var content = result?.ToString() ?? "";

            var targetRole = CheckRoutingMarkers(content);
            var response = new AgentResponse
            {
                Success = true,
                Content = content,
                Data = new Dictionary<string, object>
                {
                    ["roleType"] = RoleType,
                    ["model"] = modelName
                }
            };

            if (targetRole != null)
                response.TargetRole = targetRole;
            else if (_config.Routing?.DefaultNext != null)
                response.TargetRole = _config.Routing.DefaultNext;

            Status = AgentStatus.Idle;
            return response;
        }
        catch (Exception ex)
        {
            Status = AgentStatus.Error;
            Logger.LogError(ex, "Agent {RoleType} processing failed", RoleType);
            return new AgentResponse
            {
                Success = false,
                Content = $"处理失败: {ex.Message}",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    private string? CheckRoutingMarkers(string? content)
    {
        if (string.IsNullOrEmpty(content) || _config.Routing?.Markers == null)
            return null;

        foreach (var (marker, targetRole) in _config.Routing.Markers)
        {
            if (content.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Routing marker [{Marker}] detected, routing to {Target}", marker, targetRole);
                return targetRole;
            }
        }

        return null;
    }
}
