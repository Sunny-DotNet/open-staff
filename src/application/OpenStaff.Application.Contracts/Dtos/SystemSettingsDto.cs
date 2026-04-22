using System.Text.Json.Serialization;

namespace OpenStaff.Dtos;

/// <summary>
/// 类型化系统设置 DTO。
/// Typed DTO that represents the persisted system settings.
/// </summary>
public class SystemSettingsDto
{
    /// <summary>团队名称，显示在页面标题和全局上下文中。 / Team name shown in page titles and shared agent context.</summary>
    [JsonPropertyName("teamName")]
    public string TeamName { get; set; } = "OpenStaff";

    /// <summary>团队描述，作为所有智能体共享的团队背景说明。 / Team description used as shared background context for all agents.</summary>
    [JsonPropertyName("teamDescription")]
    public string TeamDescription { get; set; } = string.Empty;

    /// <summary>智能体对用户的称呼。 / Preferred way for agents to address the user.</summary>
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = "主人";

    /// <summary>默认回复语言，例如 zh-CN 或 en-US。 / Default reply language such as zh-CN or en-US.</summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "zh-CN";

    /// <summary>默认时区标识，例如 Asia/Shanghai。 / Default timezone identifier such as Asia/Shanghai.</summary>
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "Asia/Shanghai";

    /// <summary>默认温度参数，通常取值 0.0 到 2.0。 / Default temperature value, typically between 0.0 and 2.0.</summary>
    [JsonPropertyName("defaultTemperature")]
    public double DefaultTemperature { get; set; } = 0.7;

    /// <summary>默认最大输出 Token 数。 / Default maximum number of output tokens.</summary>
    [JsonPropertyName("defaultMaxTokens")]
    public int DefaultMaxTokens { get; set; } = 4096;

    /// <summary>默认回复风格，例如 concise、balanced 或 detailed。 / Default response style such as concise, balanced, or detailed.</summary>
    [JsonPropertyName("responseStyle")]
    public string ResponseStyle { get; set; } = "balanced";

    /// <summary>ProjectGroup 能力申请是否自动审批。 / Whether ProjectGroup capability requests should be auto-approved.</summary>
    [JsonPropertyName("autoApproveProjectGroupCapabilities")]
    public bool AutoApproveProjectGroupCapabilities { get; set; }
}
