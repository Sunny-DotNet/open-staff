namespace OpenStaff.Core.Events;

/// <summary>
/// 事件发布器接口 / Event publisher interface
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(AgentEventData eventData, CancellationToken cancellationToken = default);
}

/// <summary>
/// 事件数据传输对象 / Event data transfer object
/// </summary>
public class AgentEventData
{
    public Guid ProjectId { get; set; }
    public Guid? AgentId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Metadata { get; set; }
}
