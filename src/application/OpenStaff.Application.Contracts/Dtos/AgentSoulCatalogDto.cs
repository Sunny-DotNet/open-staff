namespace OpenStaff.Dtos;

/// <summary>
/// 灵魂配置选项。
/// Selectable option exposed by the agent-soul catalog.
/// </summary>
public class AgentSoulOptionDto
{
    /// <summary>稳定键。 / Stable key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>当前语言下的显示文案。 / Localized display label.</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// 灵魂配置选项集。
/// Grouped soul-option catalog returned to the frontend.
/// </summary>
public class AgentSoulCatalogDto
{
    /// <summary>性格特征。 / Personality traits.</summary>
    public List<AgentSoulOptionDto> Traits { get; set; } = [];

    /// <summary>工作态度。 / Work attitudes.</summary>
    public List<AgentSoulOptionDto> Attitudes { get; set; } = [];

    /// <summary>沟通风格。 / Communication styles.</summary>
    public List<AgentSoulOptionDto> Styles { get; set; } = [];
}
