using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenStaff.Agent;
using OpenStaff.Agent.Builtin;
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
            Source = input.Source,
            ProviderType = input.ProviderType,
            ModelProviderId = string.IsNullOrEmpty(input.ModelProviderId) ? null : Guid.Parse(input.ModelProviderId),
            ModelName = input.ModelName,
            Config = input.Config,
            Soul = MapSoulFromDto(input.Soul),
            IsBuiltin = false,
            IsActive = true
        };

        // 为非内置角色自动合成 SystemPrompt
        role.SystemPrompt = BuildSystemPrompt(role);

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
            if (input.Soul != null) role.Soul = MapSoulFromDto(input.Soul);
        }
        else
        {
            if (input.Name != null) role.Name = input.Name;
            if (input.Description != null) role.Description = input.Description;
            if (!string.IsNullOrEmpty(input.ModelProviderId))
            {
                var providerId = Guid.Parse(input.ModelProviderId);
                role.ModelProviderId = providerId == Guid.Empty ? null : providerId;
            }
            if (input.ModelName != null) role.ModelName = input.ModelName;
            if (input.Config != null) role.Config = input.Config;
            if (input.Soul != null) role.Soul = MapSoulFromDto(input.Soul);

            role.SystemPrompt = BuildSystemPrompt(role);
        }

        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

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

    public async Task<Guid> TestChatAsync(TestChatRequest request, CancellationToken ct)
    {
        var id = request.AgentRoleId;
        var message = request.Message;
        var liveOverride = request.Override;

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required");

        var role = await _db.AgentRoles.FirstOrDefaultAsync(r => r.Id == id && r.IsActive, ct);
        if (role == null) throw new KeyNotFoundException("Agent role not found");

        // 解析供应商和密钥（支持 Override 覆盖）
        var providerId = !string.IsNullOrEmpty(liveOverride?.ModelProviderId)
            ? Guid.Parse(liveOverride.ModelProviderId)
            : role.ModelProviderId;

        ProviderAccount? account = null;
        string? apiKey = null;
        string? endpointOverride = null;

        if (providerId.HasValue)
        {
            var resolved = await _providerResolver.ResolveAsync(providerId.Value, ct);
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

        // 解析角色配置（支持 Override 覆盖）
        var builtinProvider = _agentFactory.Providers.GetValueOrDefault("builtin") as BuiltinAgentProvider;
        var roleConfig = builtinProvider?.GetRoleConfig(role.RoleType);

        // 如果 Override 带了 Soul/Name/Description，用 BuildSystemPrompt 生成覆盖的 systemPrompt
        string systemPrompt;
        if (liveOverride?.Soul != null || liveOverride?.Name != null || liveOverride?.Description != null)
        {
            var tempRole = new AgentRole
            {
                Name = liveOverride?.Name ?? role.Name,
                Description = liveOverride?.Description ?? role.Description,
                Soul = MapSoulFromDto(liveOverride?.Soul) ?? role.Soul,
            };
            systemPrompt = BuildSystemPrompt(tempRole);
        }
        else
        {
            systemPrompt = !string.IsNullOrEmpty(role.SystemPrompt) ? role.SystemPrompt : roleConfig?.SystemPrompt ?? "";
        }

        var modelName = liveOverride?.ModelName
            ?? (!string.IsNullOrEmpty(role.ModelName) ? role.ModelName : roleConfig?.ModelName ?? "gpt-4o");
        var temperature = liveOverride?.Temperature;

        var sessionId = Guid.NewGuid();
        var stream = _streamManager.Create(sessionId);

        stream.Push(SessionEventTypes.UserInput, payload: JsonSerializer.Serialize(new { content = message }));

        // 查询 MCP 绑定
        var mcpBindings = await _db.AgentRoleMcpConfigs
            .Include(b => b.McpServerConfig)
            .Where(b => b.AgentRoleId == id && b.McpServerConfig!.IsEnabled)
            .ToListAsync(ct);

        _ = Task.Run(() => RunTestChatStream(
            stream, sessionId, account, apiKey, modelName, endpointOverride,
            systemPrompt, message, temperature, mcpBindings, role));

        return sessionId;
    }

    private async Task RunTestChatStream(
        SessionStream stream,
        Guid sessionId,
        ProviderAccount account,
        string apiKey,
        string modelName,
        string? endpointOverride,
        string systemPrompt,
        string userMessage,
        double? temperature,
        List<AgentRoleMcpConfig> mcpBindings,
        AgentRole role)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var bgCt = cts.Token;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var chatClient = _chatClientFactory.Create(
                account.ProtocolType, apiKey, modelName, endpointOverride);

            // 加载 MCP 工具
            var mcpTools = await LoadMcpToolsAsync(stream, mcpBindings, bgCt);

            var chatOptions = new ChatOptions();
            if (mcpTools.Count > 0) chatOptions.Tools = mcpTools;
            if (temperature.HasValue) chatOptions.Temperature = (float)temperature.Value;

            var chatMessages = new List<Microsoft.Extensions.AI.ChatMessage>();
            if (!string.IsNullOrEmpty(systemPrompt))
                chatMessages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, systemPrompt));
            chatMessages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, userMessage));

            // 流式推送（含工具调用循环）
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
                    ProcessStreamingUpdate(update, stream, fullContent, thinkingContent,
                        ref inputTokens, ref outputTokens, ref firstTokenMs, stopwatch, pendingToolCalls);
                }

                if (pendingToolCalls.Count == 0) break;

                await ExecuteToolCallsAsync(chatMessages, pendingToolCalls, mcpTools, stream, bgCt);
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
                    usage = new { inputTokens, outputTokens, totalTokens = (inputTokens ?? 0) + (outputTokens ?? 0) },
                    timing = new { totalMs = stopwatch.ElapsedMilliseconds, firstTokenMs }
                }));

            _streamManager.CompleteTransient(sessionId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            stream.Push(SessionEventTypes.Error,
                payload: JsonSerializer.Serialize(new { error = ex.Message }));
            _streamManager.CompleteTransient(sessionId);
        }
    }

    private async Task<List<AITool>> LoadMcpToolsAsync(
        SessionStream stream, List<AgentRoleMcpConfig> mcpBindings, CancellationToken ct)
    {
        var mcpTools = new List<AITool>();
        foreach (var binding in mcpBindings)
        {
            try
            {
                var tools = await _mcpClientManager.ListToolsAsync(binding.McpServerConfig!, ct);
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
        return mcpTools;
    }

    private static void ProcessStreamingUpdate(
        ChatResponseUpdate update,
        SessionStream stream,
        StringBuilder fullContent,
        StringBuilder thinkingContent,
        ref int? inputTokens,
        ref int? outputTokens,
        ref long? firstTokenMs,
        System.Diagnostics.Stopwatch stopwatch,
        List<FunctionCallContent> pendingToolCalls)
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

    private static async Task ExecuteToolCallsAsync(
        List<Microsoft.Extensions.AI.ChatMessage> chatMessages,
        List<FunctionCallContent> pendingToolCalls,
        List<AITool> mcpTools,
        SessionStream stream,
        CancellationToken ct)
    {
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
                var tool = mcpTools.FirstOrDefault(t => t.Name == toolCall.Name);
                if (tool is AIFunction aiFunc)
                {
                    var result = await aiFunc.InvokeAsync(
                        toolCall.Arguments != null ? new AIFunctionArguments(toolCall.Arguments) : null, ct);
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

    public List<VendorSchemaDto> GetVendorSchemas()
    {
        return _agentFactory.Providers.Values
            .Where(p => p.ProviderType != "builtin")
            .Select(p =>
        {
            var schema = p.GetConfigSchema();
            return new VendorSchemaDto
            {
                ProviderType = p.ProviderType,
                DisplayName = p.DisplayName,
                Fields = schema.Fields.Select(f => new VendorFieldDto
                {
                    Key = f.Key,
                    Label = f.Label,
                    FieldType = f.FieldType.ToString().ToLowerInvariant(),
                    Required = f.Required,
                    DefaultValue = f.DefaultValue,
                    Placeholder = f.Placeholder,
                    Options = f.Options?.Select(o => new VendorFieldOptionDto
                    {
                        Value = o.Value,
                        Label = o.Label
                    }).ToList()
                }).ToList()
            };
        }).ToList();
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
        Source = role.Source,
        ProviderType = role.ProviderType,
        Config = role.Config,
        Soul = role.Soul != null ? new AgentSoulDto
        {
            Traits = role.Soul.Traits,
            Style = role.Soul.Style,
            Attitudes = role.Soul.Attitudes,
            Custom = role.Soul.Custom
        } : null,
        CreatedAt = role.CreatedAt
    };

    private static Core.Models.AgentSoul? MapSoulFromDto(AgentSoulDto? dto)
    {
        if (dto == null) return null;
        return new Core.Models.AgentSoul
        {
            Traits = dto.Traits ?? [],
            Style = dto.Style,
            Attitudes = dto.Attitudes ?? [],
            Custom = dto.Custom
        };
    }

    private static string BuildSystemPrompt(AgentRole role)
    {
        var sb = new StringBuilder();
        sb.AppendLine("以下是你的身份信息");

        if (!string.IsNullOrEmpty(role.Name))
            sb.AppendLine($"名称:```{role.Name}```");
        if (!string.IsNullOrEmpty(role.Description))
            sb.AppendLine($"职务说明:```{role.Description}```");

        if (role.Soul != null)
        {
            if (role.Soul.Traits.Count > 0)
                sb.AppendLine($"性格特征:{string.Join(',', role.Soul.Traits.Select(t => $"```{t}```"))}");
            if (!string.IsNullOrEmpty(role.Soul.Style))
                sb.AppendLine($"沟通风格:```{role.Soul.Style}```");
            if (role.Soul.Attitudes.Count > 0)
                sb.AppendLine($"工作态度:{string.Join(',', role.Soul.Attitudes.Select(a => $"```{a}```"))}");
            if (!string.IsNullOrEmpty(role.Soul.Custom))
                sb.AppendLine($"其它补充:```{role.Soul.Custom}```");
        }

        return sb.ToString();
    }
}
