using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Core.Orchestration;
using OpenStaff.Core.Services;
using OpenStaff.Agents.Architect.Prompts;

namespace OpenStaff.Agents.Architect;

/// <summary>
/// 架构者智能体 — 将需求/决策分解为具体任务，分析依赖关系
/// Architect agent — decomposes requirements/decisions into tasks with dependency analysis
/// </summary>
public class ArchitectAgent : AgentBase
{
    public override string RoleType => BuiltinRoleTypes.Architect;

    public ArchitectAgent(ILogger<ArchitectAgent> logger) : base(logger) { }

    public override async Task<AgentResponse> ProcessAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        Status = AgentStatus.Thinking;

        try
        {
            await PublishEventAsync(EventTypes.Thought, "正在分析需求并分解任务… / Analyzing requirements and decomposing tasks…");

            if (Context?.ModelClient is null)
            {
                Status = AgentStatus.Error;
                return new AgentResponse { Success = false, Content = "LLM 客户端未配置 / LLM client not configured" };
            }

            var projectContext = Context.Project != null
                ? $"项目: {Context.Project.Name}, 技术栈: {Context.Project.TechStack}"
                : null;

            var request = new ChatRequest
            {
                Model = Context.Role?.ModelName ?? "gpt-4o",
                Temperature = 0.4,
                Messages = new List<ChatMessage>
                {
                    new() { Role = "system", Content = ArchitectPrompts.SystemPrompt },
                    new() { Role = "user", Content = ArchitectPrompts.BuildDecompositionPrompt(message.Content, projectContext) }
                }
            };

            var response = await Context.ModelClient.ChatAsync(request, cancellationToken);

            // 解析并验证任务图 / Parse and validate task graph
            var taskPlan = ParseAndValidateTaskPlan(response.Content);

            await PublishEventAsync(EventTypes.Action,
                $"任务分解完成，共 {taskPlan.TaskCount} 个任务 / Task decomposition complete: {taskPlan.TaskCount} tasks",
                response.Content);

            Status = AgentStatus.Idle;

            return new AgentResponse
            {
                Success = true,
                Content = response.Content,
                NextAction = "execute_tasks",
                TargetRole = BuiltinRoleTypes.Orchestrator,
                Data =
                {
                    ["type"] = "task_plan",
                    ["taskCount"] = taskPlan.TaskCount,
                    ["hasCycles"] = taskPlan.HasCycles
                }
            };
        }
        catch (Exception ex)
        {
            Status = AgentStatus.Error;
            Logger.LogError(ex, "架构者处理失败 / Architect processing failed");
            await PublishEventAsync(EventTypes.Error, ex.Message);
            return new AgentResponse { Success = false, Content = ex.Message, Errors = { ex.Message } };
        }
    }

    private TaskPlanResult ParseAndValidateTaskPlan(string llmOutput)
    {
        var result = new TaskPlanResult();

        try
        {
            var json = ExtractJson(llmOutput);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tasks", out var tasksElement))
                return result;

            var graph = new TaskGraph();
            var tasks = tasksElement.EnumerateArray().ToList();
            result.TaskCount = tasks.Count;

            // 为 string id 生成稳定的 Guid 映射 / Create stable Guid mapping for string ids
            var idMap = new Dictionary<string, Guid>();
            foreach (var task in tasks)
            {
                var strId = task.GetProperty("id").GetString() ?? $"task-{idMap.Count}";
                idMap[strId] = Guid.NewGuid();
                var title = task.GetProperty("title").GetString() ?? "untitled";
                var priority = task.TryGetProperty("priority", out var p) ? p.GetInt32() : 0;
                graph.AddTask(idMap[strId], title, priority);
            }

            foreach (var task in tasks)
            {
                var strId = task.GetProperty("id").GetString() ?? "";
                if (!idMap.ContainsKey(strId)) continue;
                if (task.TryGetProperty("dependencies", out var deps))
                {
                    foreach (var dep in deps.EnumerateArray())
                    {
                        var depStrId = dep.GetString();
                        if (!string.IsNullOrEmpty(depStrId) && idMap.TryGetValue(depStrId, out var depGuid))
                            graph.AddDependency(idMap[strId], depGuid);
                    }
                }
            }

            result.HasCycles = graph.HasCycle();
            if (result.HasCycles)
                Logger.LogWarning("任务图存在循环依赖！ / Task graph has circular dependencies!");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "解析任务计划失败 / Failed to parse task plan");
        }

        return result;
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private class TaskPlanResult
    {
        public int TaskCount { get; set; }
        public bool HasCycles { get; set; }
    }
}
