using Microsoft.AspNetCore.SignalR;

namespace OpenStaff.Api.Hubs;

/// <summary>
/// 智能体实时通信 Hub / Agent real-time communication hub
/// </summary>
public class AgentHub : Hub
{
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(ILogger<AgentHub> logger)
    {
        _logger = logger;
    }

    /// <summary>加入工程频道 / Join project channel</summary>
    public async Task JoinProject(Guid projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"project-{projectId}");
        _logger.LogInformation("客户端 {ConnectionId} 加入工程 {ProjectId}", Context.ConnectionId, projectId);
    }

    /// <summary>离开工程频道 / Leave project channel</summary>
    public async Task LeaveProject(Guid projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project-{projectId}");
    }

    /// <summary>发送消息给智能体 / Send message to agent</summary>
    public async Task SendToAgent(Guid projectId, Guid agentId, string message)
    {
        // 广播给同一工程的所有客户端
        await Clients.Group($"project-{projectId}").SendAsync("UserMessage", new
        {
            ProjectId = projectId,
            AgentId = agentId,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    // 服务端推送方法签名（由后端服务调用）:
    // AgentThinking(projectId, agentId, thought)
    // AgentMessage(projectId, fromAgent, toAgent, message)
    // TaskStatusChanged(projectId, taskId, status)
    // CheckpointCreated(projectId, checkpoint)
    // UserInputRequired(projectId, agentId, question)
    // AgentStatusChanged(projectId, agentId, status)
}
