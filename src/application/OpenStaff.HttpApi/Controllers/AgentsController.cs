
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using OpenStaff.ApiServices;
using OpenStaff.Dtos;

namespace OpenStaff.HttpApi.Controllers;

/// <summary>
/// 项目智能体控制器。
/// Controller that exposes project agent assignment and messaging endpoints.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/agents")]
public class AgentsController : ControllerBase
{
    private readonly IAgentApiService _agentApiService;
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// 初始化项目智能体控制器。
    /// Initializes the project agents controller.
    /// </summary>
    public AgentsController(IAgentApiService agentApiService, IHostEnvironment hostEnvironment)
    {
        _agentApiService = agentApiService;
        _hostEnvironment = hostEnvironment;
    }

    /// <summary>
    /// 获取项目已分配的智能体。
    /// Gets the agents assigned to a project.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AgentDto>>> GetAll(Guid projectId, CancellationToken ct)
        => Ok(await _agentApiService.GetProjectAgentsAsync(projectId, ct));

    /// <summary>
    /// 更新项目智能体分配。
    /// Updates the agent assignments for a project.
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<ApiMessageDto>> SetAgents(Guid projectId, [FromBody] SetAgentsBody body, CancellationToken ct)
    {
        await _agentApiService.SetProjectAgentsAsync(new SetProjectAgentsRequest
        {
            ProjectId = projectId,
            AgentRoleIds = body.AgentRoleIds
        }, ct);
        return Ok(new ApiMessageDto { Message = "ok" });
    }

    /// <summary>
    /// 获取单个智能体的事件列表。
    /// Gets the event feed for a single project agent.
    /// </summary>
    [HttpGet("{agentId:guid}/events")]
    public async Task<ActionResult<PagedAgentEventsDto>> GetEvents(Guid projectId, Guid agentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _agentApiService.GetEventsAsync(new GetAgentEventsRequest
        {
            ProjectId = projectId,
            ProjectAgentRoleId = agentId,
            Page = page,
            PageSize = pageSize
        }, ct));

    /// <summary>
    /// 向指定智能体发送消息。
    /// Sends a message to the specified agent.
    /// </summary>
    [HttpPost("{agentId:guid}/message")]
    public async Task<ActionResult<ConversationTaskOutput>> SendMessage(Guid projectId, Guid agentId, [FromBody] SendMessageBody body, CancellationToken ct)
    {
        var result = await _agentApiService.SendMessageAsync(new SendAgentMessageRequest
        {
            ProjectId = projectId,
            ProjectAgentRoleId = agentId,
            Message = body.Message
        }, ct);
        return Ok(result);
    }

    /// <summary>
    /// 获取项目成员的运行时预览。
    /// Gets the runtime preview for a project agent.
    /// </summary>
    [HttpGet("{agentId:guid}/runtime-preview")]
    public async Task<ActionResult<AgentRuntimePreviewDto>> GetRuntimePreview(Guid projectId, Guid agentId, CancellationToken ct)
    {
        if (!_hostEnvironment.IsDevelopment())
            return NotFound();

        return Ok(await _agentApiService.GetRuntimePreviewAsync(projectId, agentId, ct));
    }
}

/// <summary>
/// 更新智能体分配的请求体。
/// Request body for updating agent assignments.
/// </summary>
public class SetAgentsBody
{
    /// <summary>要分配的角色标识列表。 / Agent role identifiers to assign.</summary>
    public List<Guid> AgentRoleIds { get; set; } = [];
}

/// <summary>
/// 向智能体发送消息的请求体。
/// Request body for sending a message to an agent.
/// </summary>
public class SendMessageBody
{
    /// <summary>消息正文。 / Message body.</summary>
    public string Message { get; set; } = string.Empty;
}

