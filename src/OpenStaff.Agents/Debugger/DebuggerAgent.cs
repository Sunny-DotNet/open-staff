using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Core.Services;
using OpenStaff.Agents.Debugger.Prompts;

namespace OpenStaff.Agents.Debugger;

/// <summary>
/// 调试者智能体 — 代码分析、测试编写与执行、Bug 诊断
/// Debugger agent — code analysis, test writing/execution, bug diagnosis
/// </summary>
public class DebuggerAgent : AgentBase
{
    public override string RoleType => BuiltinRoleTypes.Debugger;

    public DebuggerAgent(ILogger<DebuggerAgent> logger) : base(logger) { }

    public override async Task<AgentResponse> ProcessAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        Status = AgentStatus.Thinking;

        try
        {
            await PublishEventAsync(EventTypes.Thought, "正在分析代码… / Analyzing code…");

            if (Context?.ModelClient is null)
            {
                Status = AgentStatus.Error;
                return new AgentResponse { Success = false, Content = "LLM 客户端未配置 / LLM client not configured" };
            }

            // 判断任务类型 / Determine task type
            var isTestRun = message.MessageType == "command" && message.Content.Contains("run_tests");

            if (isTestRun)
            {
                return await RunTestsAndReportAsync(message, cancellationToken);
            }

            // 默认：代码审查和问题分析 / Default: code review and issue analysis
            return await AnalyzeCodeAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            Status = AgentStatus.Error;
            Logger.LogError(ex, "调试者处理失败 / Debugger processing failed");
            await PublishEventAsync(EventTypes.Error, ex.Message);
            return new AgentResponse { Success = false, Content = ex.Message, Errors = { ex.Message } };
        }
    }

    private async Task<AgentResponse> AnalyzeCodeAsync(AgentMessage message, CancellationToken cancellationToken)
    {
        var request = new ChatRequest
        {
            Model = Context!.Role?.ModelName ?? "gpt-4o",
            Temperature = 0.3,
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = DebuggerPrompts.SystemPrompt },
                new() { Role = "user", Content = DebuggerPrompts.BuildAnalysisPrompt(message.Content) }
            }
        };

        var response = await Context.ModelClient.ChatAsync(request, cancellationToken);

        await PublishEventAsync(EventTypes.Action, response.Content);

        Status = AgentStatus.Idle;

        // 检查是否发现问题 / Check if issues were found
        var hasIssues = response.Content.Contains("\"severity\":\"high\"") ||
                        response.Content.Contains("\"severity\":\"critical\"");

        if (hasIssues)
        {
            return new AgentResponse
            {
                Success = true,
                Content = response.Content,
                NextAction = "fix_issues",
                TargetRole = BuiltinRoleTypes.Producer,
                Data = { ["type"] = "bug_report" }
            };
        }

        return new AgentResponse
        {
            Success = true,
            Content = response.Content,
            NextAction = "complete",
            TargetRole = BuiltinRoleTypes.Orchestrator
        };
    }

    private async Task<AgentResponse> RunTestsAndReportAsync(AgentMessage message, CancellationToken cancellationToken)
    {
        Status = AgentStatus.Working;
        await PublishEventAsync(EventTypes.Action, "正在运行测试… / Running tests…");

        var workspacePath = Context?.Project?.WorkspacePath ?? "";
        if (string.IsNullOrEmpty(workspacePath))
        {
            return new AgentResponse { Success = false, Content = "工作空间路径未配置 / Workspace path not configured" };
        }

        var runner = new TestRunner(Logger);
        var result = await runner.RunDotnetTestsAsync(workspacePath);

        var summary = $"测试结果: {result.PassedTests}/{result.TotalTests} 通过\n" +
                      $"Test result: {result.PassedTests}/{result.TotalTests} passed";

        await PublishEventAsync(EventTypes.Action, summary,
            JsonSerializer.Serialize(new { result.TotalTests, result.PassedTests, result.FailedTests, result.Success }));

        Status = AgentStatus.Idle;

        if (!result.Success && result.FailedTests.Count > 0)
        {
            // 让 LLM 分析失败原因 / Let LLM analyze failure reasons
            var analysis = await AnalyzeTestFailuresAsync(result, cancellationToken);

            return new AgentResponse
            {
                Success = false,
                Content = $"{summary}\n\n{analysis}",
                NextAction = "fix_tests",
                TargetRole = BuiltinRoleTypes.Producer,
                Data = { ["type"] = "test_failures", ["output"] = result.Output }
            };
        }

        return new AgentResponse
        {
            Success = true,
            Content = summary,
            NextAction = "complete",
            TargetRole = BuiltinRoleTypes.Orchestrator
        };
    }

    private async Task<string> AnalyzeTestFailuresAsync(TestResult testResult, CancellationToken cancellationToken)
    {
        if (Context?.ModelClient is null) return "无法分析 / Cannot analyze";

        var request = new ChatRequest
        {
            Model = Context.Role?.ModelName ?? "gpt-4o-mini",
            Temperature = 0.3,
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = "你是一名测试专家。分析以下测试输出，诊断失败原因并给出修复建议。" },
                new() { Role = "user", Content = $"测试输出:\n{testResult.Output}\n\n失败的测试:\n{string.Join("\n", testResult.FailedTests)}" }
            }
        };

        var response = await Context.ModelClient.ChatAsync(request, cancellationToken);
        return response.Content;
    }
}
