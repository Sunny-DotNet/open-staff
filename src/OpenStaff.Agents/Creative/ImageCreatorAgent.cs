using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;

namespace OpenStaff.Agents.Creative;

/// <summary>
/// 图像创作者智能体 — 占位实现，待集成 DALL-E / Stable Diffusion
/// Image creator agent — placeholder for future DALL-E / Stable Diffusion integration
/// </summary>
public class ImageCreatorAgent : AgentBase
{
    public override string RoleType => BuiltinRoleTypes.ImageCreator;

    public ImageCreatorAgent(ILogger<ImageCreatorAgent> logger) : base(logger) { }

    public override Task<AgentResponse> ProcessAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        Status = AgentStatus.Idle;

        // TODO: 集成 DALL-E / Stable Diffusion API
        // TODO: Integrate DALL-E / Stable Diffusion API
        return Task.FromResult(new AgentResponse
        {
            Success = false,
            Content = "图像生成功能尚未实现，将在后续版本中支持 DALL-E 和 Stable Diffusion。\n" +
                      "Image generation is not yet implemented. DALL-E and Stable Diffusion support coming in a future version."
        });
    }
}
