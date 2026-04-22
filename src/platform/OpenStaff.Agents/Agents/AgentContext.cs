using OpenStaff.Entities;
using OpenStaff.Core.Notifications;

namespace OpenStaff.Core.Agents;

/// <summary>
/// 智能体上下文 / Agent execution context shared during a single run.
/// </summary>
public class AgentContext
{
    /// <summary>当前关联的项目标识；全局场景下可为空 / Project identifier for the current run; may be null for global scenarios.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>当前关联的会话标识；无显式会话时可为空 / Session identifier for the current run; may be null when no explicit session exists.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>当前执行智能体实例的标识 / Identifier of the executing agent instance.</summary>
    public Guid AgentInstanceId { get; set; }

    /// <summary>当前关联的项目成员标识；仅项目运行场景存在 / Project-agent identifier for project execution flows.</summary>
    public Guid? ProjectAgentRoleId { get; set; }

    /// <summary>当前角色定义 / Role definition selected for the run.</summary>
    public AgentRole Role { get; set; } = null!;

    /// <summary>当前项目聚合根 / Project aggregate for the current run.</summary>
    public Project Project { get; set; } = null!;

    /// <summary>运行时使用的供应商账户 / Provider account used to create the runtime agent.</summary>
    public ProviderAccount? Account { get; set; }

    /// <summary>已解密的 API 密钥 / Decrypted API key.</summary>
    public string? ApiKey { get; set; }

    /// <summary>可选通知服务 / Optional notification service for streaming updates.</summary>
    public INotificationService? NotificationService { get; set; }

    /// <summary>交互语言代码 / Language code used for the interaction.</summary>
    public string Language { get; set; } = "zh-CN";

    /// <summary>运行时附加配置 / Extra runtime configuration values.</summary>
    public Dictionary<string, object> ExtraConfig { get; set; } = new();

    /// <summary>当前会话场景 / Current conversation scene.</summary>
    public SceneType? Scene { get; set; }
}
