using Microsoft.Extensions.Logging;

namespace OpenStaff.Core.Agents;

/// <summary>
/// 智能体基类 / Agent base class
/// </summary>
public abstract class AgentBase : IAgent
{
    protected AgentContext? Context { get; private set; }
    protected ILogger Logger { get; }

    public abstract string RoleType { get; }
    public string Status { get; protected set; } = Models.AgentStatus.Idle;

    protected AgentBase(ILogger logger)
    {
        Logger = logger;
    }

    public virtual Task InitializeAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        Context = context;
        Status = Models.AgentStatus.Idle;
        Logger.LogInformation("智能体 {RoleType} 初始化完成 / Agent {RoleType} initialized", RoleType, RoleType);
        return Task.CompletedTask;
    }

    public abstract Task<AgentResponse> ProcessAsync(AgentMessage message, CancellationToken cancellationToken = default);

    public virtual Task StopAsync(CancellationToken cancellationToken = default)
    {
        Status = Models.AgentStatus.Idle;
        Logger.LogInformation("智能体 {RoleType} 已停止 / Agent {RoleType} stopped", RoleType, RoleType);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发布事件 / Publish an event
    /// </summary>
    protected async Task PublishEventAsync(string eventType, string content, string? metadata = null)
    {
        if (Context?.EventPublisher != null)
        {
            await Context.EventPublisher.PublishAsync(new Events.AgentEventData
            {
                ProjectId = Context.ProjectId,
                AgentId = Context.AgentInstanceId,
                EventType = eventType,
                Content = content,
                Metadata = metadata
            });
        }
    }
}
