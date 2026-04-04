namespace OpenStaff.Core.Models;

/// <summary>
/// 智能体事件/消息 / Agent event
/// </summary>
public class AgentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid? AgentId { get; set; }
    public string EventType { get; set; } = string.Empty; // message/thought/decision/action/error/checkpoint
    public string? Content { get; set; }
    public string? Metadata { get; set; } // JSON: token用量/耗时等
    public Guid? ParentEventId { get; set; } // 事件链
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    public Project? Project { get; set; }
    public ProjectAgent? Agent { get; set; }
    public AgentEvent? ParentEvent { get; set; }
    public ICollection<AgentEvent> ChildEvents { get; set; } = new List<AgentEvent>();
}

public static class EventTypes
{
    public const string Message = "message"; // 角色间消息
    public const string Thought = "thought"; // 思考过程
    public const string Decision = "decision"; // 决策
    public const string Action = "action"; // 执行操作
    public const string Error = "error"; // 错误
    public const string Checkpoint = "checkpoint"; // 存储点事件
    public const string UserInput = "user_input"; // 用户输入
    public const string SystemNotice = "system_notice"; // 系统通知
}
