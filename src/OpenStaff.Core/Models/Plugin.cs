namespace OpenStaff.Core.Models;

/// <summary>
/// 插件 / Plugin
/// </summary>
public class Plugin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string Manifest { get; set; } = string.Empty; // JSON: 插件清单
    public string? AssemblyPath { get; set; } // DLL 路径
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AgentRole> AgentRoles { get; set; } = new List<AgentRole>();
}
