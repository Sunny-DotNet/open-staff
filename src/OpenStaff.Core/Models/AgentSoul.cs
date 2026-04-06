namespace OpenStaff.Core.Models;

/// <summary>
/// 智能体灵魂配置（值对象） / Agent soul configuration (value object)
/// </summary>
public class AgentSoul
{
    public List<string> Traits { get; set; } = [];
    public string? Style { get; set; }
    public List<string> Attitudes { get; set; } = [];
    public string? Custom { get; set; }
}
