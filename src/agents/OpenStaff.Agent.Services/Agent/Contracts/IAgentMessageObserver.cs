namespace OpenStaff.Agent.Services;

/// <summary>
/// zh-CN: 消费运行时事件，用于持久化、监控或下游流式投影。
/// en: Consumes runtime events for persistence, monitoring, or downstream streaming projections.
/// </summary>
public interface IAgentMessageObserver
{
    /// <summary>
    /// zh-CN: 将运行时事件发布到观察者自己的目标端。
    /// en: Publishes a runtime event to the observer-specific sink.
    /// </summary>
    Task PublishAsync(AgentMessageEvent messageEvent, CancellationToken cancellationToken);
}
