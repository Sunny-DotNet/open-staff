using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenStaff.Agents;
using OpenStaff.Application.Contracts.AgentRoles;
using OpenStaff.Application.Contracts.AgentRoles.Dtos;
using OpenStaff.Application.Providers;
using OpenStaff.Application.Sessions;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Application.AgentRoles;

public class AgentRoleAppService : IAgentRoleAppService
{
    private readonly AppDbContext _db;
    private readonly ProviderAccountService _accountService;
    private readonly AgentFactory _agentFactory;
    private readonly ChatClientFactory _chatClientFactory;
    private readonly IProviderResolver _providerResolver;
    private readonly SessionStreamManager _streamManager;
    private readonly McpServers.McpClientManager _mcpClientManager;

    public AgentRoleAppService(
        AppDbContext db,
        ProviderAccountService accountService,
        AgentFactory agentFactory,
        ChatClientFactory chatClientFactory,
        IProviderResolver providerResolver,
        SessionStreamManager streamManager,
        McpServers.McpClientManager mcpClientManager)
    {
        _db = db;
        _accountService = accountService;
        _agentFactory = agentFactory;
        _chatClientFactory = chatClientFactory;
        _providerResolver = providerResolver;
        _streamManager = streamManager;
        _mcpClientManager = mcpClientManager;
    }

    public async Task<List<AgentRoleDto>> GetAllAsync(CancellationToken ct)
    {
        var roles = await _db.AgentRoles
            .Where(r => r.IsActive)
            .OrderBy(r => r.IsBuiltin ? 0 : 1)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);

        foreach (var role in roles)
        {
            if (role.ModelProviderId.HasValue)
                role.ProviderAccount = await _accountService.GetByIdAsync(role.ModelProviderId.Value);
        }

        return roles.Select(MapToDto).ToList();
    }

    public async Task<AgentRoleDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var role = await _db.AgentRoles
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive, ct);
        if (role == null) return null;

        if (role.ModelProviderId.HasValue)
            role.ProviderAccount = await _accountService.GetByIdAsync(role.ModelProviderId.Value);

        return MapToDto(role);
    }

    public async Task<AgentRoleDto> CreateAsync(CreateAgentRoleInput input, CancellationToken ct)
    {
        var role = new AgentRole
        {
            Id = Guid.NewGuid(),
            Name = input.Name,
            RoleType = input.RoleType,
            Description = input.Description,
            SystemPrompt = input.SystemPrompt,
            ModelProviderId = string.IsNullOrEmpty(input.ModelProviderId) ? null : Guid.Parse(input.ModelProviderId),
            ModelName = input.ModelName,
            Config = input.Config,
            IsBuiltin = false,
            IsActive = true
        };

        _db.AgentRoles.Add(role);
        await _db.SaveChangesAsync(ct);
        return MapToDto(role);
    }

    public async Task<AgentRoleDto?> UpdateAsync(Guid id, UpdateAgentRoleInput input, CancellationToken ct)
    {
        var role = await _db.AgentRoles.FindAsync(new object[] { id }, ct);
        if (role == null) return null;

        if (role.IsBuiltin)
        {
            if (!string.IsNullOrEmpty(input.ModelProviderId))
            {
                var providerId = Guid.Parse(input.ModelProviderId);
                role.ModelProviderId = providerId == Guid.Empty ? null : providerId;
            }
            if (input.ModelName != null) role.ModelName = input.ModelName;
            if (input.Config != null) role.Config = input.Config;
        }
        else
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("以下是你的身份信息");

            if (input.Name != null)
            {
                role.Name = input.Name;
                stringBuilder.AppendLine($"名称:```{role.Name}```");
            }
            if (input.Description != null)
            {
                role.Description = input.Description;
                stringBuilder.AppendLine($"职务说明:```{role.Description}```");
            }
            if (!string.IsNullOrEmpty(input.ModelProviderId))
            {
                var providerId = Guid.Parse(input.ModelProviderId);
                role.ModelProviderId = providerId == Guid.Empty ? null : providerId;
            }
            if (input.ModelName != null) role.ModelName = input.ModelName;

            if (!string.IsNullOrEmpty(input.Config))
            {
                try
                {
                    role.Config = input.Config;
                    using var doc = JsonDocument.Parse(input.Config);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("soul", out var soul))
                    {
                        if (soul.TryGetProperty("traits", out var traits) && traits.ValueKind == JsonValueKind.Array)
                        {
                            var traitString = string.Join(',', traits.EnumerateArray().Select(x => $"```{x.GetString()}```"));
                            stringBuilder.AppendLine($"性格特征:{traitString}");
                        }
                        if (soul.TryGetProperty("style", out var style) && style.ValueKind == JsonValueKind.String)
                        {
                            stringBuilder.AppendLine($"沟通风格:```{style}```");
                        }
                        if (soul.TryGetProperty("attitudes", out var attitudes) && attitudes.ValueKind == JsonValueKind.Array)
                        {
                            var attitudeString = string.Join(',', attitudes.EnumerateArray().Select(x => $"```{x.GetString()}```"));
                            stringBuilder.AppendLine($"工作态度:{attitudeString}");
                        }
                        if (soul.TryGetProperty("custom", out var custom) && custom.ValueKind == JsonValueKind.String)
                        {
                            stringBuilder.AppendLine($"其它补充:```{custom}```");
                        }
                    }
                }
                catch { /* ignore parse errors */ }
            }
            role.SystemPrompt = stringBuilder.ToString();
        }

        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _agentFactory.RegisterDbRole(role);

        if (role.ModelProviderId.HasValue)
            role.ProviderAccount = await _accountService.GetByIdAsync(role.ModelProviderId.Value);
        return MapToDto(role);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var role = await _db.AgentRoles.FindAsync(new object[] { id }, ct);
        if (role == null) return false;
        if (role.IsBuiltin) throw new InvalidOperationException("不能删除内置角色");

        role.IsActive = false;
        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Guid> TestChatAsync(Guid id, string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required");

        var role = await _db.AgentRoles.FirstOrDefaultAsync(r => r.Id == id && r.IsActive, ct);
        if (role == null) throw new KeyNotFoundException("Agent role not found");

        ProviderAccount? account = null;
        string? apiKey = null;
        string? endpointOverride = null;

        if (role.ModelProviderId.HasValue)
        {
            var resolved = await _providerResolver.ResolveAsync(role.ModelProviderId.Value, ct);
            if (resolved != null)
            {
                account = resolved.Account;
                apiKey = resolved.ApiKey;
                endpointOverride = resolved.EndpointOverride;
            }
        }

        if (account == null || string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(account == null
                ? "请先在角色配置中选择模型供应商"
                : "请先在设置页面配置供应商的 API Key");
        }

        var sessionId = Guid.NewGuid();
        var stream = _streamManager.Create(sessionId);

        stream.Push(SessionEventTypes.UserInput, payload: JsonSerializer.Serialize(new { content = message }));

        // 在 DI scope 存活时先查询 MCP 绑定和角色配置
        var mcpBindings = await _db.AgentRoleMcpConfigs
            .Include(b => b.McpServerConfig)
            .Where(b => b.AgentRoleId == id && b.McpServerConfig!.IsEnabled)
            .ToListAsync(ct);

        var roleConfig = _agentFactory.GetRoleConfig(role.RoleType);
        var systemPrompt = !string.IsNullOrEmpty(role.SystemPrompt)
            ? role.SystemPrompt
            : roleConfig?.SystemPrompt ?? "";
        var modelName = !string.IsNullOrEmpty(role.ModelName)
            ? role.ModelName
            : roleConfig?.ModelName ?? "gpt-4o";

        _ = Task.Run(async () =>
        {
            // Task.Run 脱离了 HTTP 请求生命周期，不能使用原始 ct（已被取消）
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var bgCt = cts.Token;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // 直接创建 IChatClient 做流式输出
                var chatClient = _chatClientFactory.Create(
                    account.ProtocolType, apiKey, modelName, endpointOverride);
                // 加载 MCP 工具
                var mcpTools = new List<AITool>();

                foreach (var binding in mcpBindings)
                {
                    try
                    {
                        var tools = await _mcpClientManager.ListToolsAsync(binding.McpServerConfig!, bgCt);
                        if (!string.IsNullOrEmpty(binding.ToolFilter))
                        {
                            var filter = JsonSerializer.Deserialize<string[]>(binding.ToolFilter);
                            if (filter?.Length > 0)
                                tools = tools.Where(t => filter.Contains(t.Name)).ToList();
                        }
                        mcpTools.AddRange(tools);
                    }
                    catch (Exception ex)
                    {
                        stream.Push(SessionEventTypes.Error,
                            payload: JsonSerializer.Serialize(new { error = $"MCP 工具加载失败 ({binding.McpServerConfig!.Name}): {ex.Message}" }));
                    }
                }

                var chatOptions = mcpTools.Count > 0 ? new ChatOptions { Tools = mcpTools } : null;

                var chatMessages = new List<Microsoft.Extensions.AI.ChatMessage>();
                if (!string.IsNullOrEmpty(systemPrompt))
                    chatMessages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, systemPrompt));
                chatMessages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, message));

                // 流式推送每个 token（含思考过程和用量统计），支持工具调用循环
                var fullContent = new StringBuilder();
                var thinkingContent = new StringBuilder();
                int? inputTokens = null;
                int? outputTokens = null;
                long? firstTokenMs = null;
                const int maxToolRounds = 10;

                for (int toolRound = 0; toolRound < maxToolRounds; toolRound++)
                {
                    var pendingToolCalls = new List<FunctionCallContent>();

                    await foreach (var update in chatClient.GetStreamingResponseAsync(
                        (IEnumerable<Microsoft.Extensions.AI.ChatMessage>)chatMessages, chatOptions, bgCt))
                    {
                        foreach (var thinking in update.Contents.OfType<TextReasoningContent>())
                        {
                            if (!string.IsNullOrEmpty(thinking.Text))
                            {
                                thinkingContent.Append(thinking.Text);
                                stream.Push(SessionEventTypes.StreamingThinking,
                                    payload: JsonSerializer.Serialize(new { token = thinking.Text }));
                            }
                        }

                        foreach (var part in update.Contents.OfType<TextContent>())
                        {
                            if (!string.IsNullOrEmpty(part.Text))
                            {
                                firstTokenMs ??= stopwatch.ElapsedMilliseconds;
                                fullContent.Append(part.Text);
                                stream.Push(SessionEventTypes.StreamingToken,
                                    payload: JsonSerializer.Serialize(new { token = part.Text }));
                            }
                        }

                        foreach (var fc in update.Contents.OfType<FunctionCallContent>())
                        {
                            pendingToolCalls.Add(fc);
                        }

                        foreach (var usage in update.Contents.OfType<Microsoft.Extensions.AI.UsageContent>())
                        {
                            if (usage.Details != null)
                            {
                                inputTokens = (inputTokens ?? 0) + (int)(usage.Details.InputTokenCount ?? 0);
                                outputTokens = (outputTokens ?? 0) + (int)(usage.Details.OutputTokenCount ?? 0);
                            }
                        }
                    }

                    // 没有工具调用，结束循环
                    if (pendingToolCalls.Count == 0)
                        break;

                    // 执行工具调用并将结果加入对话历史
                    var assistantMsg = new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant,
                        pendingToolCalls.Select(fc => (AIContent)fc).ToList());
                    chatMessages.Add(assistantMsg);

                    var toolResultContents = new List<AIContent>();
                    foreach (var toolCall in pendingToolCalls)
                    {
                        stream.Push(SessionEventTypes.ToolCall,
                            payload: JsonSerializer.Serialize(new { name = toolCall.Name, arguments = toolCall.Arguments }));

                        try
                        {
                            // 查找并执行工具
                            var tool = mcpTools.FirstOrDefault(t => t.Name == toolCall.Name);
                            if (tool is AIFunction aiFunc)
                            {
                                var result = await aiFunc.InvokeAsync(
                                    toolCall.Arguments != null ? new AIFunctionArguments(toolCall.Arguments) : null, bgCt);
                                var resultStr = result?.ToString() ?? "";
                                toolResultContents.Add(new FunctionResultContent(toolCall.CallId, resultStr));
                                stream.Push(SessionEventTypes.ToolResult,
                                    payload: JsonSerializer.Serialize(new { name = toolCall.Name, result = resultStr.Length > 500 ? resultStr[..500] + "..." : resultStr }));
                            }
                            else
                            {
                                toolResultContents.Add(new FunctionResultContent(toolCall.CallId, "Tool not found"));
                            }
                        }
                        catch (Exception toolEx)
                        {
                            toolResultContents.Add(new FunctionResultContent(toolCall.CallId, $"Error: {toolEx.Message}"));
                            stream.Push(SessionEventTypes.ToolError,
                                payload: JsonSerializer.Serialize(new { name = toolCall.Name, error = toolEx.Message }));
                        }
                    }

                    chatMessages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Tool, toolResultContents));
                }

                stopwatch.Stop();

                stream.Push(SessionEventTypes.StreamingDone,
                    payload: JsonSerializer.Serialize(new
                    {
                        role = role.RoleType,
                        roleName = role.Name,
                        model = modelName,
                        content = fullContent.ToString(),
                        thinking = thinkingContent.Length > 0 ? thinkingContent.ToString() : null,
                        usage = new
                        {
                            inputTokens,
                            outputTokens,
                            totalTokens = (inputTokens ?? 0) + (outputTokens ?? 0)
                        },
                        timing = new
                        {
                            totalMs = stopwatch.ElapsedMilliseconds,
                            firstTokenMs
                        }
                    }));

                _streamManager.CompleteTransient(sessionId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                stream.Push(SessionEventTypes.Error, payload: JsonSerializer.Serialize(new
                {
                    error = ex.Message
                }));
                _streamManager.CompleteTransient(sessionId);
            }
        });

        return sessionId;
    }

    private static AgentRoleDto MapToDto(AgentRole role) => new()
    {
        Id = role.Id,
        Name = role.Name,
        RoleType = role.RoleType,
        Description = role.Description,
        SystemPrompt = role.SystemPrompt,
        ModelProviderId = role.ModelProviderId?.ToString(),
        ModelProviderName = role.ProviderAccount?.Name,
        ModelName = role.ModelName,
        IsBuiltin = role.IsBuiltin,
        Config = role.Config,
        CreatedAt = role.CreatedAt
    };
}
