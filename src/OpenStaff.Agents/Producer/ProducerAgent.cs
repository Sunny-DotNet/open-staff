using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Core.Services;
using OpenStaff.Agents.Producer.Prompts;

namespace OpenStaff.Agents.Producer;

/// <summary>
/// 生产者智能体 — 代码生成、文件操作、Git 提交
/// Producer agent — code generation, file operations, Git commits
/// </summary>
public class ProducerAgent : AgentBase
{
    public override string RoleType => BuiltinRoleTypes.Producer;

    private readonly CodeGenerator _codeGen;
    private readonly GitManager _gitManager;

    public ProducerAgent(ILogger<ProducerAgent> logger) : base(logger)
    {
        _codeGen = new CodeGenerator(logger);
        _gitManager = new GitManager(logger);
    }

    public override async Task<AgentResponse> ProcessAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        Status = AgentStatus.Working;

        try
        {
            await PublishEventAsync(EventTypes.Thought, "正在准备编码… / Preparing to code…");

            if (Context?.ModelClient is null)
            {
                Status = AgentStatus.Error;
                return new AgentResponse { Success = false, Content = "LLM 客户端未配置 / LLM client not configured" };
            }

            var workspacePath = Context.Project?.WorkspacePath ?? "";
            if (string.IsNullOrEmpty(workspacePath))
            {
                Status = AgentStatus.Error;
                return new AgentResponse { Success = false, Content = "工作空间路径未配置 / Workspace path not configured" };
            }

            // 获取项目文件列表 / Get project file listing
            var existingFiles = string.Join("\n", _codeGen.ListFiles(workspacePath, "."));

            // 构建带工具定义的请求 / Build request with tool definitions
            var request = new ChatRequest
            {
                Model = Context.Role?.ModelName ?? "gpt-4o",
                Temperature = 0.2,
                Messages = new List<ChatMessage>
                {
                    new() { Role = "system", Content = ProducerPrompts.SystemPrompt },
                    new()
                    {
                        Role = "user",
                        Content = ProducerPrompts.BuildCodingPrompt(
                            message.Attachments?.ContainsKey("taskTitle") == true ? message.Attachments["taskTitle"]?.ToString() ?? "" : "编码任务",
                            message.Content,
                            $"项目: {Context.Project?.Name}",
                            existingFiles)
                    }
                },
                Tools = GetToolDefinitions()
            };

            // 多轮工具调用循环 / Multi-turn tool-call loop
            var filesChanged = new List<string>();
            var maxIterations = 10;

            for (var i = 0; i < maxIterations; i++)
            {
                var response = await Context.ModelClient.ChatAsync(request, cancellationToken);

                // 如果没有工具调用，返回最终结果 / If no tool calls, return final result
                if (response.ToolCalls == null || response.ToolCalls.Count == 0)
                {
                    // 提交 Git / Commit to Git
                    var commitSha = CommitAndCheckpoint(workspacePath, message.Content, filesChanged);

                    await PublishEventAsync(EventTypes.Action,
                        $"编码完成，修改了 {filesChanged.Count} 个文件 / Coding complete, {filesChanged.Count} files changed",
                        JsonSerializer.Serialize(new { filesChanged, commitSha }));

                    Status = AgentStatus.Idle;

                    return new AgentResponse
                    {
                        Success = true,
                        Content = response.Content,
                        NextAction = filesChanged.Count > 0 ? "run_tests" : null,
                        TargetRole = filesChanged.Count > 0 ? BuiltinRoleTypes.Debugger : null,
                        Data =
                        {
                            ["filesChanged"] = filesChanged,
                            ["commitSha"] = commitSha ?? ""
                        }
                    };
                }

                // 处理工具调用 / Process tool calls
                request.Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = response.Content,
                    Name = "producer"
                });

                foreach (var toolCall in response.ToolCalls)
                {
                    var result = ExecuteToolCall(workspacePath, toolCall);
                    if (toolCall.Name is "create_file" or "edit_file" && result.StartsWith("✅"))
                    {
                        var args = JsonSerializer.Deserialize<Dictionary<string, string>>(toolCall.Arguments);
                        if (args?.TryGetValue("path", out var path) == true)
                            filesChanged.Add(path);
                    }

                    request.Messages.Add(new ChatMessage
                    {
                        Role = "tool",
                        Content = result,
                        ToolCallId = toolCall.Id
                    });
                }
            }

            Status = AgentStatus.Idle;
            return new AgentResponse { Success = true, Content = "达到最大迭代次数 / Reached max iterations" };
        }
        catch (Exception ex)
        {
            Status = AgentStatus.Error;
            Logger.LogError(ex, "生产者处理失败 / Producer processing failed");
            await PublishEventAsync(EventTypes.Error, ex.Message);
            return new AgentResponse { Success = false, Content = ex.Message, Errors = { ex.Message } };
        }
    }

    private string ExecuteToolCall(string workspacePath, ToolCall toolCall)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, string>>(toolCall.Arguments)
                ?? new Dictionary<string, string>();

            return toolCall.Name switch
            {
                "create_file" => _codeGen.CreateFile(workspacePath, args.GetValueOrDefault("path", ""), args.GetValueOrDefault("content", ""))
                    ? $"✅ 文件已创建: {args.GetValueOrDefault("path", "")}"
                    : $"❌ 创建失败: {args.GetValueOrDefault("path", "")}",

                "edit_file" => _codeGen.EditFile(workspacePath, args.GetValueOrDefault("path", ""),
                    args.GetValueOrDefault("old_content", ""), args.GetValueOrDefault("new_content", ""))
                    ? $"✅ 文件已编辑: {args.GetValueOrDefault("path", "")}"
                    : $"❌ 编辑失败: {args.GetValueOrDefault("path", "")}",

                "read_file" => _codeGen.ReadFile(workspacePath, args.GetValueOrDefault("path", ""))
                    ?? "❌ 文件读取失败",

                "list_files" => string.Join("\n", _codeGen.ListFiles(workspacePath, args.GetValueOrDefault("directory", "."))),

                _ => $"❌ 未知工具: {toolCall.Name}"
            };
        }
        catch (Exception ex)
        {
            return $"❌ 工具调用出错: {ex.Message}";
        }
    }

    private string? CommitAndCheckpoint(string workspacePath, string taskDescription, List<string> filesChanged)
    {
        if (filesChanged.Count == 0) return null;

        var message = $"feat: {taskDescription.Substring(0, Math.Min(72, taskDescription.Length))}";
        var sha = _gitManager.CommitChanges(workspacePath, message);

        if (sha != null)
        {
            _ = PublishEventAsync(EventTypes.Checkpoint, $"Git 提交: {sha[..8]}", JsonSerializer.Serialize(new
            {
                commitSha = sha,
                filesChanged,
                description = taskDescription
            }));
        }

        return sha;
    }

    private static List<ToolDefinition> GetToolDefinitions() => new()
    {
        new ToolDefinition
        {
            Name = "create_file",
            Description = "创建新文件 / Create a new file",
            ParametersSchema = """{"type":"object","properties":{"path":{"type":"string","description":"相对文件路径"},"content":{"type":"string","description":"文件内容"}},"required":["path","content"]}"""
        },
        new ToolDefinition
        {
            Name = "edit_file",
            Description = "编辑现有文件 / Edit an existing file",
            ParametersSchema = """{"type":"object","properties":{"path":{"type":"string"},"old_content":{"type":"string","description":"要替换的内容"},"new_content":{"type":"string","description":"替换后的内容"}},"required":["path","old_content","new_content"]}"""
        },
        new ToolDefinition
        {
            Name = "read_file",
            Description = "读取文件内容 / Read file content",
            ParametersSchema = """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}"""
        },
        new ToolDefinition
        {
            Name = "list_files",
            Description = "列出目录内容 / List directory contents",
            ParametersSchema = """{"type":"object","properties":{"directory":{"type":"string","description":"相对目录路径，默认 '.'"}},"required":[]}"""
        }
    };
}
