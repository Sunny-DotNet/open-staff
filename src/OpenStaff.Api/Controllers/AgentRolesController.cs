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
