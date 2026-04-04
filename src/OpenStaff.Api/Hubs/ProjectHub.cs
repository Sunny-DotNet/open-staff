using Microsoft.AspNetCore.SignalR;

namespace OpenStaff.Api.Hubs;

/// <summary>
/// 工程进度推送 Hub / Project progress push hub
/// </summary>
public class ProjectHub : Hub
{
    /// <summary>订阅工程列表更新 / Subscribe to project list updates</summary>
    public async Task SubscribeProjectList()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "project-list");
    }
}
