namespace OpenStaff.Entities;

/// <summary>
/// 角色测试场景下对 skill 的实际使用绑定 / Actual usage binding between an agent role and a managed skill for test chat.
/// </summary>
public class AgentRoleSkillBinding:EntityBase<Guid>
{
    /// <summary>角色标识 / Agent-role identifier.</summary>
    public Guid AgentRoleId { get; set; }

    /// <summary>Skill 安装键 / Managed skill installation key.</summary>
    public string SkillInstallKey { get; set; } = string.Empty;

    /// <summary>Skill 标识快照 / Skill identifier snapshot.</summary>
    public string SkillId { get; set; } = string.Empty;

    /// <summary>技能名称快照 / Skill name snapshot.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>展示名称快照 / Display name snapshot.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>来源仓库快照 / Source repository snapshot.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>仓库 owner 快照 / Repository owner snapshot.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>仓库名快照 / Repository name snapshot.</summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>GitHub 地址快照 / GitHub URL snapshot.</summary>
    public string? GithubUrl { get; set; }

    /// <summary>是否启用 / Whether the binding is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间（UTC） / Last update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>关联角色 / Linked agent role.</summary>
    public AgentRole? AgentRole { get; set; }
}
