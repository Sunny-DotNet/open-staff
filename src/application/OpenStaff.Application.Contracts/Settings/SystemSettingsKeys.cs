
namespace OpenStaff.Application.Contracts.Settings;

/// <summary>
/// 全局系统设置键常量。
/// Constant keys for persisted global system settings.
/// </summary>
public static class SystemSettingsKeys
{
    /// <summary>团队名称设置键。 / Setting key for the team name.</summary>
    public const string TeamName = "system:team_name";

    /// <summary>团队描述设置键。 / Setting key for the team description.</summary>
    public const string TeamDescription = "system:team_description";

    /// <summary>用户称呼设置键。 / Setting key for the user display name.</summary>
    public const string UserName = "system:user_name";

    /// <summary>默认语言设置键。 / Setting key for the default language.</summary>
    public const string Language = "system:language";

    /// <summary>默认时区设置键。 / Setting key for the default timezone.</summary>
    public const string Timezone = "system:timezone";

    /// <summary>默认温度设置键。 / Setting key for the default temperature.</summary>
    public const string DefaultTemperature = "agent:default_temperature";

    /// <summary>默认最大 Token 设置键。 / Setting key for the default max token count.</summary>
    public const string DefaultMaxTokens = "agent:default_max_tokens";

    /// <summary>默认回复风格设置键。 / Setting key for the default response style.</summary>
    public const string ResponseStyle = "agent:response_style";

    /// <summary>ProjectGroup 能力申请是否自动审批。 / Setting key for whether ProjectGroup capability requests are auto-approved.</summary>
    public const string ProjectGroupAutoApproveCapabilities = "project_group:auto_approve_capabilities";
}
