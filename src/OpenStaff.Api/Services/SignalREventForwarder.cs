using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenStaff.Api.Hubs;
using OpenStaff.Core.Events;

namespace OpenStaff.Api.Services;

/// <summary>
/// 事件转发服务 — 将内部事件推送到 SignalR 客户端
/// Event forwarding service — pushes internal events to SignalR clients
/// </summary>
public class SignalREventForwarder : BackgroundService
{
    private readonly IEventSubscriber _eventSubscriber;
    private readonly IHubContext<AgentHub> _agentHub;
    private readonly ILogger<SignalREventForwarder> _logger;

    public SignalREventForwarder(
        IEventSubscriber eventSubscriber,
        IHubContext<AgentHub> agentHub,
        ILogger<SignalREventForwarder> logger)
    {
        _eventSubscriber = eventSubscriber;
        _agentHub = agentHub;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 订阅所有事件，转发到 SignalR / Subscribe all events, forward to SignalR
        _eventSubscriber.SubscribeAll(async eventData =>
        {
            try
            {
                var group = _agentHub.Clients.Group($"project-{eventData.ProjectId}");

                // 根据事件类型分发不同的 SignalR 方法
                // Dispatch different SignalR methods based on event type
                switch (eventData.EventType)
                {
                    case "thought":
                        await group.SendAsync("AgentThinking", new
                        {
                            projectId = eventData.ProjectId,
                            agentId = eventData.AgentId,
                            content = eventData.Content,
                            metadata = eventData.Metadata
                        }, stoppingToken);
                        break;

                    case "message":
                        await group.SendAsync("AgentMessage", new
                        {
                            projectId = eventData.ProjectId,
                            agentId = eventData.AgentId,
                            content = eventData.Content,
                            metadata = eventData.Metadata
                        }, stoppingToken);
                        break;

                    case "decision":
                        await group.SendAsync("AgentDecision", new
                        {
                            projectId = eventData.ProjectId,
                            agentId = eventData.AgentId,
                            content = eventData.Content
                        }, stoppingToken);
                        break;

                    case "checkpoint":
                        await group.SendAsync("CheckpointCreated", new
                        {
                            projectId = eventData.ProjectId,
                            agentId = eventData.AgentId,
                            content = eventData.Content,
                            metadata = eventData.Metadata
                        }, stoppingToken);
                        break;

                    case "user_input":
                        await group.SendAsync("UserInputRequired", new
                        {
                            projectId = eventData.ProjectId,
                            agentId = eventData.AgentId,
                            content = eventData.Content
                        }, stoppingToken);
                        break;

                    case "error":
                        await group.SendAsync("AgentError", new
                        {
                            projectId = eventData.ProjectId,
                            agentId = eventData.AgentId,
                            content = eventData.Content
                        }, stoppingToken);
                        break;

                    default:
                        await group.SendAsync("AgentEvent", new
                        {
                            projectId = eventData.ProjectId,
                            agentId = eventData.AgentId,
                            eventType = eventData.EventType,
                            content = eventData.Content,
                            metadata = eventData.Metadata
                        }, stoppingToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "转发事件到 SignalR 失败 / Failed to forward event to SignalR");
            }
        });

        return Task.CompletedTask;
    }
}
