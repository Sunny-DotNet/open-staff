namespace OpenStaff.Entities;

/// <summary>
/// 智能体角色定义 / Agent role definition
/// </summary>
public class AgentRole:EntityBase<Guid>
{
    /// <summary>角色显示名称 / Display name of the role.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>角色描述 / Optional role description.</summary>
    public string? Description { get; set; }

    /// <summary>角色职位名称 / Human-readable job title.</summary>
    public string? JobTitle { get; set; }

    /// <summary>头像 Data URI / Avatar data URI.</summary>
    public string? Avatar { get; set; }

    /// <summary>绑定的模型供应商标识 / Bound model provider identifier.</summary>
    public Guid? ModelProviderId { get; set; }

    /// <summary>使用的具体模型名称 / Specific model name used by the role.</summary>
    public string? ModelName { get; set; }

    /// <summary>角色来源 / Origin of the role definition.</summary>
    public AgentSource Source { get; set; } = AgentSource.Custom;

    /// <summary>智能体供应商类型，如 builtin 或 github-copilot / Agent provider type, such as builtin or github-copilot.</summary>
    public string? ProviderType { get; set; }

    /// <summary>是否为系统内置角色 / Whether this is a system-provided role.</summary>
    public bool IsBuiltin { get; set; } = false;

    /// <summary>是否处于启用状态 / Whether the role is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>关联插件标识 / Related plugin identifier for custom roles.</summary>
    public Guid? PluginId { get; set; }

    /// <summary>额外配置 JSON / Extra role configuration stored as JSON.</summary>
    public string? Config { get; set; }

    /// <summary>人格与风格配置 / Persona and style configuration.</summary>
    public AgentSoul? Soul { get; set; }

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间（UTC） / Last update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>关联插件 / Related plugin.</summary>
    public Plugin? Plugin { get; set; }

    /// <summary>项目中的角色关联 / Project-scoped memberships based on this role.</summary>
    public ICollection<ProjectAgentRole> ProjectAgentRoles { get; set; } = [];

    /// <summary>测试场景下的 MCP 绑定 / MCP bindings used by role-level test chat.</summary>
    public ICollection<AgentRoleMcpBinding> McpBindings { get; set; } = [];

    /// <summary>测试场景下的 Skill 绑定 / Skill bindings used by role-level test chat.</summary>
    public ICollection<AgentRoleSkillBinding> SkillBindings { get; set; } = [];
}

/// <summary>
/// 内置角色类型 / Built-in role types
/// </summary>
public static class BuiltinRoleTypes
{
    /// <summary>秘书角色类型 / Secretary role type.</summary>
    public const string Secretary = "secretary"; // 秘书（唯一内置角色）
}
