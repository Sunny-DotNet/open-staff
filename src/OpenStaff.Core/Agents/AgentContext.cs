using OpenStaff.Core.Models;
using OpenStaff.Core.Notifications;

namespace OpenStaff.Core.Agents;

/// <summary>
/// 智能体上下文 / Agent execution context
/// </summary>
public class AgentContext
{
    /// <summary>工程ID / Project ID</summary>
    public Guid ProjectId { get; set; }

    /// <summary>智能体实例ID / Agent instance ID</summary>
    public Guid AgentInstanceId { get; set; }

    /// <summary>角色定义 / Role definition</summary>
    public AgentRole Role { get; set; } = null!;

    /// <summary>工程信息 / Project info</summary>
    public Project Project { get; set; } = null!;

    /// <summary>供应商账户 / Provider account (for agent creation)</summary>
    public ProviderAccount? Account { get; set; }

    /// <summary>已解密的 API Key / Decrypted API key</summary>
    public string? ApiKey { get; set; }

    /// <summary>通知服务 / Notification service</summary>
    public INotificationService? NotificationService { get; set; }

    /// <summary>交互语言 / Interaction language</summary>
    public string Language { get; set; } = "zh-CN";

    /// <summary>额外配置 / Extra configuration</summary>
    public Dictionary<string, object> ExtraConfig { get; set; } = new();
}
