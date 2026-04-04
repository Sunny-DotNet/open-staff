using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;

namespace OpenStaff.Agents.Creative;

/// <summary>
/// 视频创作者智能体 — 占位实现，待集成视频生成 API
/// Video creator agent — placeholder for future video generation integration
/// </summary>
public class VideoCreatorAgent : AgentBase
{
    public override string RoleType => BuiltinRoleTypes.VideoCreator;

    public VideoCreatorAgent(ILogger<VideoCreatorAgent> logger) : base(logger) { }

    public override Task<AgentResponse> ProcessAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        Status = AgentStatus.Idle;

        // TODO: 集成视频生成 API (Sora / Runway / Pika)
        // TODO: Integrate video generation API (Sora / Runway / Pika)
        return Task.FromResult(new AgentResponse
        {
            Success = false,
            Content = "视频生成功能尚未实现，将在后续版本中支持。\n" +
                      "Video generation is not yet implemented. Coming in a future version."
        });
    }
}
