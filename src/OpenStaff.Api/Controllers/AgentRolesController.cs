using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Api.Services;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Api.Controllers;

/// <summary>
/// 角色定义控制器 / Agent roles controller
/// </summary>
[ApiController]
[Route("api/agent-roles")]
public class AgentRolesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly FileProviderService _providerService;

    public AgentRolesController(AppDbContext db, FileProviderService providerService)
    {
        _db = db;
        _providerService = providerService;
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
                role.ModelProvider = _providerService.GetById(role.ModelProviderId.Value);
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
            role.ModelProvider = _providerService.GetById(role.ModelProviderId.Value);

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
            // 自定义角色允许修改所有字段
            if (request.Name != null) role.Name = request.Name;
            if (request.Description != null) role.Description = request.Description;
            if (request.SystemPrompt != null) role.SystemPrompt = request.SystemPrompt;
            if (request.ModelProviderId.HasValue)
                role.ModelProviderId = request.ModelProviderId.Value == Guid.Empty ? null : request.ModelProviderId;
            if (request.ModelName != null) role.ModelName = request.ModelName;
            if (request.Config != null) role.Config = request.Config;
        }

        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // Reload provider info
        if (role.ModelProviderId.HasValue)
            role.ModelProvider = _providerService.GetById(role.ModelProviderId.Value);
        return Ok(ToDto(role));
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

    /// <summary>
    /// 直接与指定角色对话测试（不经过 Orchestrator）
    /// Test chat directly with a specific role (bypasses Orchestrator)
    /// </summary>
    [HttpPost("{id:guid}/test-chat")]
    public async Task<IActionResult> TestChat(Guid id, [FromBody] TestChatRequest request, CancellationToken cancellationToken)
    {
        var role = await _db.AgentRoles
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive, cancellationToken);
        if (role == null) return NotFound();

        // 获取模型提供商配置（从文件系统）
        ModelProvider? provider = null;
        if (role.ModelProviderId.HasValue)
        {
            provider = _providerService.GetById(role.ModelProviderId.Value);
        }

        // 如果角色没有绑定模型，尝试找一个已启用的提供商
        if (provider == null)
        {
            provider = _providerService.GetFirstEnabled();
        }

        if (provider == null)
        {
            return BadRequest(new { message = "没有可用的模型提供商，请先在设置中启用至少一个提供商" });
        }

        // 解析 API Key（Copilot 会自动做 oauth_token → github_token 交换）
        var apiKeyResolver = HttpContext.RequestServices.GetRequiredService<ApiKeyResolver>();
        var resolved = await apiKeyResolver.ResolveAsync(provider, cancellationToken);
        if (string.IsNullOrEmpty(resolved.ApiKey))
        {
            return BadRequest(new { message = "模型提供商未配置 API Key" });
        }

        // 如果 Copilot token 返回了动态端点，覆盖 provider 的 BaseUrl
        var effectiveProvider = provider;
        if (!string.IsNullOrEmpty(resolved.EndpointOverride))
        {
            effectiveProvider = new ModelProvider
            {
                Id = provider.Id,
                Name = provider.Name,
                ProviderType = provider.ProviderType,
                BaseUrl = resolved.EndpointOverride,
                DefaultModel = provider.DefaultModel,
                IsEnabled = provider.IsEnabled
            };
        }

        // 使用 AgentFactory 创建并调用 agent
        var agentFactory = HttpContext.RequestServices.GetRequiredService<OpenStaff.Agents.AgentFactory>();
        if (!agentFactory.IsRegistered(role.RoleType))
        {
            return BadRequest(new { message = $"角色类型 '{role.RoleType}' 未注册" });
        }

        var agent = agentFactory.CreateAgent(role.RoleType);

        // 创建临时上下文（Provider + ApiKey 供 AIAgentFactory 使用）
        var context = new OpenStaff.Core.Agents.AgentContext
        {
            ProjectId = Guid.Empty,
            AgentInstanceId = Guid.NewGuid(),
            Role = role,
            Project = new Project { Id = Guid.Empty, Name = "Test" },
            Provider = effectiveProvider,
            ApiKey = resolved.ApiKey,
            EventPublisher = new NullEventPublisher(),
            Language = "zh-CN"
        };

        await agent.InitializeAsync(context, cancellationToken);

        var message = new OpenStaff.Core.Agents.AgentMessage
        {
            Content = request.Message,
            FromRole = "user",
            Timestamp = DateTime.UtcNow
        };

        var response = await agent.ProcessAsync(message, cancellationToken);

        return Ok(new
        {
            success = response.Success,
            content = response.Content,
            model = role.ModelName ?? provider.DefaultModel,
            provider = provider.Name,
            errors = response.Errors
        });
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

/// <summary>
/// 空事件发布器（测试对话用）/ Null event publisher for test chats
/// </summary>
internal class NullEventPublisher : OpenStaff.Core.Events.IEventPublisher
{
    public Task PublishAsync(OpenStaff.Core.Events.AgentEventData eventData, CancellationToken ct = default)
        => Task.CompletedTask;
}
