using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Agents;
using OpenStaff.Application.Providers;
using OpenStaff.Application.Sessions;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;
using System.Text;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// 角色定义控制器 / Agent roles controller
/// </summary>
[ApiController]
[Route("api/agent-roles")]
public class AgentRolesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DbProviderService _providerService;
    private readonly AgentFactory _agentFactory;
    private readonly IProviderResolver _providerResolver;
    private readonly SessionStreamManager _streamManager;

    public AgentRolesController(
        AppDbContext db,
        DbProviderService providerService,
        AgentFactory agentFactory,
        IProviderResolver providerResolver,
        SessionStreamManager streamManager)
    {
        _db = db;
        _providerService = providerService;
        _agentFactory = agentFactory;
        _providerResolver = providerResolver;
        _streamManager = streamManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var roles = await _db.AgentRoles
            .Where(r => r.IsActive)
            .OrderBy(r => r.IsBuiltin ? 0 : 1)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        // 手动填充供应商信息
        foreach (var role in roles)
        {
            if (role.ModelProviderId.HasValue)
                role.ModelProvider = await _providerService.GetByIdAsync(role.ModelProviderId.Value);
        }

        var result = roles.Select(ToDto);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var role = await _db.AgentRoles
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive, cancellationToken);
        if (role == null) return NotFound();

        if (role.ModelProviderId.HasValue)
            role.ModelProvider = await _providerService.GetByIdAsync(role.ModelProviderId.Value);

        return Ok(ToDto(role));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAgentRoleRequest request, CancellationToken cancellationToken)
    {
        var role = new AgentRole
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            RoleType = request.RoleType,
            Description = request.Description,
            SystemPrompt = request.SystemPrompt,
            ModelProviderId = request.ModelProviderId,
            ModelName = request.ModelName,
            Config = request.Config,
            IsBuiltin = false,
            IsActive = true
        };

        _db.AgentRoles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(role));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAgentRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _db.AgentRoles.FindAsync(new object[] { id }, cancellationToken);
        if (role == null) return NotFound();

        if (role.IsBuiltin)
        {
            // 内置角色只允许修改模型相关配置
            if (request.ModelProviderId.HasValue)
                role.ModelProviderId = request.ModelProviderId.Value == Guid.Empty ? null : request.ModelProviderId;
            if (request.ModelName != null) role.ModelName = request.ModelName;
            if (request.Config != null) role.Config = request.Config;
        }
        else
        {

            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("以下是你的身份信息");
            // 自定义角色允许修改所有字段
            if (request.Name != null) {
                role.Name = request.Name;
                stringBuilder.AppendLine($"名称:```{role.Name}```");
            }
            if (request.Description != null)
            {
                role.Description = request.Description;
                stringBuilder.AppendLine($"职务说明:```{role.Description}```");
            }
            if (request.ModelProviderId.HasValue)
                role.ModelProviderId = request.ModelProviderId.Value == Guid.Empty ? null : request.ModelProviderId;
            if (request.ModelName != null) role.ModelName = request.ModelName;


            // 解析 config JSON 中的 modelParameters 和 tools
            if (!string.IsNullOrEmpty(request.Config))
            {
                try
                {
                        role.Config = request.Config;
                        using var doc = System.Text.Json.JsonDocument.Parse(request.Config);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("soul", out var soul))
                    {
                        if (soul.TryGetProperty("traits", out var traits)&& traits.ValueKind== System.Text.Json.JsonValueKind.Array) {
                            var traitString = string.Join(',', traits.EnumerateArray().Select(x => $"```{x.GetString()}```"));

                            stringBuilder.AppendLine($"性格特征:{traitString}");
                        }
                        if (soul.TryGetProperty("style", out var style) && style.ValueKind == System.Text.Json.JsonValueKind.String)
                        {

                            stringBuilder.AppendLine($"沟通风格:```{style}```");
                        }
                        if (soul.TryGetProperty("attitudes", out var attitudes) && attitudes.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            var attitudeString = string.Join(',', attitudes.EnumerateArray().Select(x => $"```{x.GetString()}```"));
                            stringBuilder.AppendLine($"工作态度:{attitudeString}");
                        }
                        if (soul.TryGetProperty("custom", out var custom) && custom.ValueKind == System.Text.Json.JsonValueKind.String)
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
        await _db.SaveChangesAsync(cancellationToken);

        // 同步更新 AgentFactory 中的 DB 角色缓存
        _agentFactory.RegisterDbRole(role);

        // Reload provider info
        if (role.ModelProviderId.HasValue)
            role.ModelProvider = await _providerService.GetByIdAsync(role.ModelProviderId.Value);
        return Ok(ToDto(role));
    }

    /// <summary>
    /// 测试代理体对话（异步） — 返回 sessionId，前端通过 SignalR StreamSession 订阅结果
    /// </summary>
    [HttpPost("{id:guid}/test-chat")]
    public async Task<IActionResult> TestChat(Guid id, [FromBody] TestChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required" });

        var role = await _db.AgentRoles.FirstOrDefaultAsync(r => r.Id == id && r.IsActive, cancellationToken);
        if (role == null) return NotFound(new { error = "Agent role not found" });

        // 解析模型供应商和 API Key
        ModelProvider? provider = null;
        string? apiKey = null;

        if (role.ModelProviderId.HasValue)
        {
            var resolved = await _providerResolver.ResolveAsync(role.ModelProviderId.Value, cancellationToken);
            if (resolved != null)
            {
                provider = resolved.Provider;
                apiKey = resolved.ApiKey;
            }
        }

        if (provider == null || string.IsNullOrEmpty(apiKey))
        {
            return BadRequest(new
            {
                error = "模型供应商未配置或 API Key 未设置",
                detail = provider == null
                    ? "请先在角色配置中选择模型供应商"
                    : "请先在设置页面配置供应商的 API Key"
            });
        }

        // 创建会话流，立即返回 sessionId
        var sessionId = Guid.NewGuid();
        var stream = _streamManager.Create(sessionId);

        // 记录用户输入事件
        stream.Push(SessionEventTypes.UserInput, payload: System.Text.Json.JsonSerializer.Serialize(new { content = request.Message }));

        // 后台处理 Agent 调用
        _ = Task.Run(async () =>
        {
            try
            {
                var agent = _agentFactory.CreateAgentFromDbRole(role);

                var context = new AgentContext
                {
                    ProjectId = Guid.Empty,
                    AgentInstanceId = Guid.NewGuid(),
                    Role = new AgentRole
                    {
                        RoleType = role.RoleType,
                        Name = role.Name,
                        ModelName = role.ModelName
                    },
                    Provider = provider,
                    ApiKey = apiKey,
                    Language = "zh-CN"
                };
                await agent.InitializeAsync(context);

                var message = new AgentMessage
                {
                    Content = request.Message,
                    FromRole = "user",
                    Timestamp = DateTime.UtcNow
                };

                var response = await agent.ProcessAsync(message, CancellationToken.None);

                // 推送 Agent 回复
                stream.Push(SessionEventTypes.Message, payload: System.Text.Json.JsonSerializer.Serialize(new
                {
                    role = role.RoleType,
                    roleName = role.Name,
                    content = response.Content,
                    success = response.Success,
                    targetRole = response.TargetRole,
                    errors = response.Errors
                }));

                // 完成会话流（不持久化，保留给迟到的订阅者回放）
                _streamManager.CompleteTransient(sessionId);
            }
            catch (Exception ex)
            {
                stream.Push(SessionEventTypes.Error, payload: System.Text.Json.JsonSerializer.Serialize(new
                {
                    error = ex.Message
                }));
                _streamManager.CompleteTransient(sessionId);
            }
        });

        return Ok(new { sessionId });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var role = await _db.AgentRoles.FindAsync(new object[] { id }, cancellationToken);
        if (role == null) return NotFound();
        if (role.IsBuiltin) return BadRequest(new { message = "不能删除内置角色" });

        role.IsActive = false;
        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static object ToDto(AgentRole role)
    {
        return new
        {
            role.Id,
            role.Name,
            roleType = role.RoleType,
            role.Description,
            role.SystemPrompt,
            modelProviderId = role.ModelProviderId,
            role.ModelName,
            modelProviderName = role.ModelProvider?.Name,
            isBuiltin = role.IsBuiltin,
            role.Config,
            role.CreatedAt,
            role.UpdatedAt
        };
    }
}

public class CreateAgentRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string RoleType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public Guid? ModelProviderId { get; set; }
    public string? ModelName { get; set; }
    public string? Config { get; set; }
}

public class UpdateAgentRoleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public Guid? ModelProviderId { get; set; }
    public string? ModelName { get; set; }
    public string? Config { get; set; }
}

public class TestChatRequest
{
    public string Message { get; set; } = string.Empty;
}
