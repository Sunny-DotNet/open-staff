namespace OpenStaff.Core.Models;

/// <summary>
/// 智能体角色定义 / Agent role definition
/// </summary>
public class AgentRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string RoleType { get; set; } = string.Empty; // 角色类型标识
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; } // 系统提示词
    public Guid? ModelProviderId { get; set; } // 绑定的模型供应商
    public string? ModelName { get; set; } // 使用的具体模型
    public AgentSource Source { get; set; } = AgentSource.Custom;
    public string? ProviderType { get; set; } // 智能体供应商类型（"builtin", "anthropic", "google", "github-copilot"）
    public bool IsBuiltin { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public Guid? PluginId { get; set; } // 关联插件(自定义角色)
    public string? Config { get; set; } // JSON 额外配置
    public AgentSoul? Soul { get; set; } // 灵魂配置（EF OwnsOne JSON）
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 运行时属性（不持久化，由 OrchestrationService 解析后赋值）
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public ProviderAccount? ProviderAccount { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? ApiKey { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? EndpointOverride { get; set; }
    public Plugin? Plugin { get; set; }
    public ICollection<ProjectAgent> ProjectAgents { get; set; } = new List<ProjectAgent>();
}

/// <summary>
/// 内置角色类型 / Built-in role types
/// </summary>
public static class BuiltinRoleTypes
{
    public const string Secretary = "secretary"; // 秘书（唯一内置角色）
}
