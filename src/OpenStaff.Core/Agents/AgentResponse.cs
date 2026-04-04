namespace OpenStaff.Core.Agents;

/// <summary>
/// 智能体响应 / Agent response
/// </summary>
public class AgentResponse
{
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? NextAction { get; set; } // 建议的下一步操作
    public string? TargetRole { get; set; } // 建议传递给哪个角色
    public Dictionary<string, object> Data { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
