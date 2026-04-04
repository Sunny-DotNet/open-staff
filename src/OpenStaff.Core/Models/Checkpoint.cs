namespace OpenStaff.Core.Models;

/// <summary>
/// 代码存储点 / Code checkpoint
/// </summary>
public class Checkpoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? AgentId { get; set; }
    public string? GitCommitSha { get; set; }
    public string? Description { get; set; }
    public string? DiffSummary { get; set; }
    public string? FilesChanged { get; set; } // JSON: 变更文件列表
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    public Project? Project { get; set; }
    public TaskItem? Task { get; set; }
    public ProjectAgent? Agent { get; set; }
}
