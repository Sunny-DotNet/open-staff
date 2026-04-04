namespace OpenStaff.Core.Agents;

/// <summary>
/// 智能体消息 / Agent message
/// </summary>
public class AgentMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FromRole { get; set; } = string.Empty; // 来源角色类型 or "user"
    public Guid? FromAgentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text"; // text/command/data
    public Dictionary<string, object> Attachments { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
