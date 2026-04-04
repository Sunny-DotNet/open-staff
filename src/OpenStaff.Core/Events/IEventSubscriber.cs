namespace OpenStaff.Core.Events;

/// <summary>
/// 事件订阅器接口 / Event subscriber interface
/// </summary>
public interface IEventSubscriber
{
    /// <summary>
    /// 订阅工程事件 / Subscribe to project events
    /// </summary>
    IDisposable Subscribe(Guid projectId, Func<AgentEventData, Task> handler);

    /// <summary>
    /// 订阅全局事件 / Subscribe to all events
    /// </summary>
    IDisposable SubscribeAll(Func<AgentEventData, Task> handler);
}
