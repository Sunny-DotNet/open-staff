using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Core.Services;

namespace OpenStaff.Agents;

/// <summary>
/// 标准智能体 — 所有角色使用同一实现 / Standard agent — single implementation for all roles
/// </summary>
public class StandardAgent : AgentBase
{
    private readonly RoleConfig _config;
    private readonly IAgentToolRegistry _toolRegistry;
    private readonly IPromptLoader _promptLoader;
    private const int MaxToolIterations = 10;

    public StandardAgent(
        RoleConfig config,
        IAgentToolRegistry toolRegistry,
        IPromptLoader promptLoader,
        ILogger<StandardAgent> logger) : base(logger)
    {
        _config = config;
        _toolRegistry = toolRegistry;
        _promptLoader = promptLoader;
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

            // 2. Get available tools
            var tools = _toolRegistry.GetTools(_config.Tools);

            // 3. Publish thinking event
            var preview = message.Content?.Length > 100
                ? message.Content.Substring(0, 100) + "..."
                : message.Content ?? "";
            await PublishEventAsync(EventTypes.Thought, $"[{_config.Name}] 正在处理: {preview}");

            // 4. Run LLM with tool-calling loop
            var result = await RunToolLoopAsync(systemPrompt, message, tools, cancellationToken);

            // 5. Check routing markers
            var targetRole = CheckRoutingMarkers(result.Content);
            if (targetRole != null)
            {
                result.TargetRole = targetRole;
            }
            else if (_config.Routing?.DefaultNext != null)
            {
                result.TargetRole = _config.Routing.DefaultNext;
            }

            Status = AgentStatus.Idle;
            return result;
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
    /// 通用 tool-calling 循环 / Generic tool-calling loop
    /// </summary>
    private async Task<AgentResponse> RunToolLoopAsync(
        string systemPrompt, AgentMessage message,
        IReadOnlyList<IAgentTool> tools, CancellationToken ct)
    {
        var modelClient = Context?.ModelClient;
        if (modelClient == null)
        {
            return new AgentResponse
            {
                Success = false,
                Content = "ModelClient not configured",
                Errors = new List<string> { "No ModelClient in context" }
            };
        }

        var modelName = _config.ModelName ?? Context?.Role?.ModelName ?? "gpt-4o";
        var temperature = _config.ModelParameters?.Temperature ?? 0.7;

        // Build chat request using the proper IModelClient interface
        var request = new ChatRequest
        {
            Model = modelName,
            Temperature = temperature,
            MaxTokens = _config.ModelParameters?.MaxTokens,
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = message.Content ?? "" }
            }
        };

        // Add tool definitions if available
        if (tools.Count > 0)
        {
            request.Tools = tools.Select(t => new ToolDefinition
            {
                Name = t.Name,
                Description = t.Description,
                ParametersSchema = t.ParametersSchema
            }).ToList();
        }

        // Tool-calling loop
        for (var iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            var response = await modelClient.ChatAsync(request, ct);

            // If LLM wants to call tools
            if (response.ToolCalls is { Count: > 0 })
            {
                // Add assistant message with tool calls
                request.Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = response.Content ?? ""
                });

                foreach (var toolCall in response.ToolCalls)
                {
                    var tool = tools.FirstOrDefault(t =>
                        t.Name.Equals(toolCall.Name, StringComparison.OrdinalIgnoreCase));

                    string toolResult;
                    if (tool != null)
                    {
                        try
                        {
                            toolResult = await tool.ExecuteAsync(toolCall.Arguments, Context!, ct);
                            await PublishEventAsync(EventTypes.Action, $"执行工具: {tool.Name}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning(ex, "Tool {ToolName} execution failed", toolCall.Name);
                            toolResult = $"Error: {ex.Message}";
                        }
                    }
                    else
                    {
                        toolResult = $"Error: Tool '{toolCall.Name}' not found";
                    }

                    // Add tool result to messages
                    request.Messages.Add(new ChatMessage
                    {
                        Role = "tool",
                        Content = toolResult,
                        ToolCallId = toolCall.Id,
                        Name = toolCall.Name
                    });
                }

                // Continue loop to let LLM process tool results
                continue;
            }

            // No tool calls — return final response
            return new AgentResponse
            {
                Success = true,
                Content = response.Content ?? "",
                Data = new Dictionary<string, object>
                {
                    ["roleType"] = RoleType,
                    ["model"] = modelName
                }
            };
        }

        // Exceeded max iterations
        return new AgentResponse
        {
            Success = true,
            Content = "已达到最大工具迭代次数 / Max tool iterations reached",
            Data = new Dictionary<string, object>
            {
                ["roleType"] = RoleType,
                ["model"] = modelName
            }
        };
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
