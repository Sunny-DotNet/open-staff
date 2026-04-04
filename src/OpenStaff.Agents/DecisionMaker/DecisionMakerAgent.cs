using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Core.Services;
using OpenStaff.Agents.DecisionMaker.Prompts;

namespace OpenStaff.Agents.DecisionMaker;

/// <summary>
/// 决策者智能体 — 评估技术方案并做出决策
/// Decision maker agent — evaluates technical options and makes decisions
/// </summary>
public class DecisionMakerAgent : AgentBase
{
    public override string RoleType => BuiltinRoleTypes.DecisionMaker;

    public DecisionMakerAgent(ILogger<DecisionMakerAgent> logger) : base(logger) { }

    public override async Task<AgentResponse> ProcessAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        Status = AgentStatus.Thinking;

        try
        {
            await PublishEventAsync(EventTypes.Thought, "正在评估技术方案… / Evaluating technical options…");

            if (Context?.ModelClient is null)
            {
                Status = AgentStatus.Error;
                return new AgentResponse { Success = false, Content = "LLM 客户端未配置 / LLM client not configured" };
            }

            var projectContext = Context.Project != null
                ? $"项目: {Context.Project.Name}, 技术栈: {Context.Project.TechStack}"
                : "";

            var request = new ChatRequest
            {
                Model = Context.Role?.ModelName ?? "gpt-4o",
                Temperature = 0.3,
                Messages = new List<ChatMessage>
                {
                    new() { Role = "system", Content = DecisionMakerPrompts.SystemPrompt },
                    new() { Role = "user", Content = DecisionMakerPrompts.BuildDecisionPrompt(message.Content, projectContext) }
                }
            };

            var response = await Context.ModelClient.ChatAsync(request, cancellationToken);

            await PublishEventAsync(EventTypes.Decision, response.Content);

            Status = AgentStatus.Idle;

            return new AgentResponse
            {
                Success = true,
                Content = response.Content,
                NextAction = "decompose_tasks",
                TargetRole = BuiltinRoleTypes.Architect,
                Data = { ["type"] = "decision_report" }
            };
        }
        catch (Exception ex)
        {
            Status = AgentStatus.Error;
            Logger.LogError(ex, "决策者处理失败 / Decision maker processing failed");
            await PublishEventAsync(EventTypes.Error, ex.Message);
            return new AgentResponse { Success = false, Content = ex.Message, Errors = { ex.Message } };
        }
    }
}
