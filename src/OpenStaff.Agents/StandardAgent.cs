using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Agents.Tools;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using AgentResponse = OpenStaff.Core.Agents.AgentResponse;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace OpenStaff.Agents;

/// <summary>
/// 标准智能体 — 所有角色使用同一实现，内部委托 AIAgent 处理 LLM 交互
/// Standard agent — single implementation for all roles, delegates to AIAgent for LLM interaction
/// </summary>
public class StandardAgent : AgentBase
{
    private readonly RoleConfig _config;
    private readonly IAgentToolRegistry _toolRegistry;
    private readonly IPromptLoader _promptLoader;
    private readonly AIAgentFactory _aiAgentFactory;

    public StandardAgent(
        RoleConfig config,
        IAgentToolRegistry toolRegistry,
        IPromptLoader promptLoader,
        AIAgentFactory aiAgentFactory,
        ILogger<StandardAgent> logger) : base(logger)
    {
        _config = config;
        _toolRegistry = toolRegistry;
        _promptLoader = promptLoader;
        _aiAgentFactory = aiAgentFactory;
    }

    public override string RoleType => _config.RoleType;

    public override async Task<AgentResponse> ProcessAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        Status = AgentStatus.Thinking;
        try
        {
            // 1. Load system prompt by language
            var language = Context?.Language ?? "zh-CN";
            var systemPrompt = _promptLoader.Load(_config.SystemPrompt, language);

            // 2. Publish thinking event
            var preview = message.Content?.Length > 100
                ? message.Content.Substring(0, 100) + "..."
                : message.Content ?? "";
            await PublishEventAsync(EventTypes.Thought, $"[{_config.Name}] 正在处理: {preview}");

            // 3. Validate provider context
            if (Context?.Provider == null || string.IsNullOrEmpty(Context.ApiKey))
            {
                return new AgentResponse
                {
                    Success = false,
                    Content = "模型供应商未配置 / Model provider not configured",
                    Errors = new List<string> { "No provider or API key in context" }
                };
            }

            // 4. Resolve tools from registry
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

            // 5. Create AIAgent via factory with resolved provider + tools
            var modelName = _config.ModelName ?? Context!.Role?.ModelName ?? "gpt-4o";
            var aiAgent = _aiAgentFactory.CreateAgent(
                Context!.Provider!,
                Context!.ApiKey!,
                modelName: modelName,
                instructions: systemPrompt,
                agentName: _config.Name,
                tools: aiTools);

            // 6. Build chat messages
            var chatMessages = new List<ChatMessage>
            {
                new(ChatRole.User, message.Content ?? "")
            };

            // 7. Run via AIAgent (handles tool-calling loop internally)
            var result = await aiAgent.RunAsync(chatMessages, session: null, options: null, cancellationToken: cancellationToken);
            var content = result?.ToString() ?? "";

            // 8. Check routing markers
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
            {
                response.TargetRole = targetRole;
            }
            else if (_config.Routing?.DefaultNext != null)
            {
                response.TargetRole = _config.Routing.DefaultNext;
            }

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

    /// <summary>
    /// 检查路由标记 / Check routing markers in response
    /// </summary>
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
