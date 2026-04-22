namespace OpenStaff.Entities;

/// <summary>
/// 插件 / Plugin
/// </summary>
public class Plugin:EntityBase<Guid>
{
    /// <summary>插件名称 / Plugin name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>插件版本 / Plugin version.</summary>
    public string? Version { get; set; }

    /// <summary>插件说明 / Plugin description.</summary>
    public string? Description { get; set; }

    /// <summary>插件清单 JSON / Plugin manifest stored as JSON.</summary>
    public string Manifest { get; set; } = string.Empty;

    /// <summary>插件程序集路径 / Path to the plugin assembly.</summary>
    public string? AssemblyPath { get; set; }

    /// <summary>是否启用 / Whether the plugin is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间（UTC） / Last update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>插件提供的角色定义 / Agent roles contributed by the plugin.</summary>
    public ICollection<AgentRole> AgentRoles { get; set; } = new List<AgentRole>();
}
