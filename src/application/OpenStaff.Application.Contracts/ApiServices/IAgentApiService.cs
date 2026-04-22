
using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// 项目智能体应用服务契约。
/// Application service contract for project agent assignments and agent events.
/// </summary>
public interface IAgentApiService : IApiServiceBase
{
    /// <summary>
    /// 获取项目已分配的智能体。
    /// Gets the agents assigned to a project.
    /// </summary>
    Task<List<AgentDto>> GetProjectAgentsAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// 替换项目的智能体分配列表。
    /// Replaces the set of agent roles assigned to a project.
    /// </summary>
    Task SetProjectAgentsAsync(SetProjectAgentsRequest request, CancellationToken ct = default);

    /// <summary>
    /// 获取指定智能体的事件分页。
    /// Gets a paged event feed for a specific project agent.
    /// </summary>
    Task<PagedAgentEventsDto> GetEventsAsync(GetAgentEventsRequest request, CancellationToken ct = default);

    /// <summary>
    /// 向指定智能体发送消息。
    /// Sends a message to a specific project agent.
    /// </summary>
    Task<ConversationTaskOutput> SendMessageAsync(SendAgentMessageRequest request, CancellationToken ct = default);

    /// <summary>
    /// 获取项目成员在当前环境下的运行时预览。
    /// Gets the runtime preview of a project agent in the current environment.
    /// </summary>
    Task<AgentRuntimePreviewDto> GetRuntimePreviewAsync(Guid projectId, Guid projectAgentRoleId, CancellationToken ct = default);
}


