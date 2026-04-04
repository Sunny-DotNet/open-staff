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
    public bool IsBuiltin { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public Guid? PluginId { get; set; } // 关联插件(自定义角色)
    public string? Config { get; set; } // JSON 额外配置
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    public ModelProvider? ModelProvider { get; set; }
    public Plugin? Plugin { get; set; }
    public ICollection<ProjectAgent> ProjectAgents { get; set; } = new List<ProjectAgent>();
}

/// <summary>
/// 内置角色类型 / Built-in role types
/// </summary>
public static class BuiltinRoleTypes
{
    public const string Orchestrator = "orchestrator"; // 调度器
    public const string Communicator = "communicator"; // 对话者
    public const string DecisionMaker = "decision_maker"; // 决策者
    public const string Architect = "architect"; // 架构者
    public const string Producer = "producer"; // 生产者
    public const string Debugger = "debugger"; // 调试者
    public const string ImageCreator = "image_creator"; // 图片创造者
    public const string VideoCreator = "video_creator"; // 视频创造者
}
