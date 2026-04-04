namespace OpenStaff.Core.Models;

/// <summary>
/// 工程模型 / Project entity
/// </summary>
public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? TechStack { get; set; } // JSON 格式的技术栈信息
    public string Language { get; set; } = "zh-CN"; // 交互语言
    public string? WorkspacePath { get; set; } // 工作空间路径
    public string? GitConfig { get; set; } // JSON: remote url, branch 等
    public string Status { get; set; } = ProjectStatus.Initializing;
    public string? Metadata { get; set; } // JSON 额外元数据
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>主群聊会话 ID</summary>
    public Guid? MainSessionId { get; set; }

    // 导航属性
    public ChatSession? MainSession { get; set; }
    public ICollection<ProjectAgent> Agents { get; set; } = new List<ProjectAgent>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<AgentEvent> Events { get; set; } = new List<AgentEvent>();
    public ICollection<Checkpoint> Checkpoints { get; set; } = new List<Checkpoint>();
}

public static class ProjectStatus
{
    public const string Initializing = "initializing";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Completed = "completed";
    public const string Archived = "archived";
}
