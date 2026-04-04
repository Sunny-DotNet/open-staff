using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Core.Services;
using OpenStaff.Agents.Communicator.Prompts;

namespace OpenStaff.Agents.Communicator;

/// <summary>
/// 对话者智能体 — 负责与用户自然语言交互、需求采集
/// Communicator agent — handles natural language interaction and requirements gathering
/// </summary>
public class CommunicatorAgent : AgentBase
{
    public override string RoleType => BuiltinRoleTypes.Communicator;

    public CommunicatorAgent(ILogger<CommunicatorAgent> logger) : base(logger) { }

    public override async Task<AgentResponse> ProcessAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        Status = AgentStatus.Thinking;

        try
        {
            await PublishEventAsync(EventTypes.Thought, "正在分析用户消息… / Analyzing user message…");

            if (Context?.ModelClient is null)
            {
                Status = AgentStatus.Error;
                return new AgentResponse { Success = false, Content = "LLM 客户端未配置 / LLM client not configured" };
            }

            var systemPrompt = Context.Language == "en-US"
                ? CommunicatorPrompts.SystemPromptEn
                : CommunicatorPrompts.SystemPromptZh;

            var request = new ChatRequest
            {
                Model = Context.Role?.ModelName ?? "gpt-4o-mini",
                Temperature = 0.7,
                Messages = new List<ChatMessage>
                {
                    new() { Role = "system", Content = systemPrompt },
                    new() { Role = "user", Content = message.Content }
                }
            };

            var response = await Context.ModelClient.ChatAsync(request, cancellationToken);

            await PublishEventAsync(EventTypes.Message, response.Content);

            Status = AgentStatus.Idle;

            // 判断是否需要进入下一步 / Decide if we should route to next agent
            var isRequirementComplete = response.Content.Contains("[REQUIREMENTS_COMPLETE]");
            var cleanContent = response.Content.Replace("[REQUIREMENTS_COMPLETE]", "").Trim();

            if (isRequirementComplete)
            {
                return new AgentResponse
                {
                    Success = true,
                    Content = cleanContent,
                    NextAction = "evaluate_requirements",
                    TargetRole = BuiltinRoleTypes.DecisionMaker,
                    Data = { ["type"] = "requirements" }
                };
            }

            return new AgentResponse
            {
                Success = true,
                Content = cleanContent,
                NextAction = "ask_user"
            };
        }
        catch (Exception ex)
        {
            Status = AgentStatus.Error;
            Logger.LogError(ex, "对话者处理失败 / Communicator processing failed");
            await PublishEventAsync(EventTypes.Error, ex.Message);
            return new AgentResponse { Success = false, Content = ex.Message, Errors = { ex.Message } };
        }
    }
}
