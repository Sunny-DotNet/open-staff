namespace OpenStaff.Entities;

/// <summary>
/// 智能体灵魂配置（值对象） / Agent soul configuration (value object)
/// </summary>
public class AgentSoul
{
    /// <summary>性格特征列表 / List of personality traits.</summary>
    public List<string> Traits { get; set; } = [];

    /// <summary>表达风格 / Communication style.</summary>
    public string? Style { get; set; }

    /// <summary>态度倾向列表 / List of attitudes or stances.</summary>
    public List<string> Attitudes { get; set; } = [];

    /// <summary>额外自定义描述 / Free-form custom description.</summary>
    public string? Custom { get; set; }
}
