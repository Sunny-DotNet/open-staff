using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public AgentRolesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var roles = await _db.AgentRoles
            .Include(r => r.ModelProvider)
            .OrderBy(r => r.IsBuiltin ? 0 : 1)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);
        return Ok(roles);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AgentRole role, CancellationToken cancellationToken)
    {
        role.Id = Guid.NewGuid();
        role.IsBuiltin = false;
        _db.AgentRoles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(role);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AgentRole updated, CancellationToken cancellationToken)
    {
        var role = await _db.AgentRoles.FindAsync(new object[] { id }, cancellationToken);
        if (role == null) return NotFound();

        role.Name = updated.Name;
        role.Description = updated.Description;
        role.SystemPrompt = updated.SystemPrompt;
        role.ModelProviderId = updated.ModelProviderId;
        role.ModelName = updated.ModelName;
        role.Config = updated.Config;
        role.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(role);
    }
}
