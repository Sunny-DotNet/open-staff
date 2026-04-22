namespace OpenStaff.Dtos;

/// <summary>
/// 设备授权启动结果。
/// Result returned when a device authorization flow starts.
/// </summary>
public class DeviceCodeDto
{
    /// <summary>用户输入到授权页面的验证码。 / User code that should be entered on the verification page.</summary>
    public string UserCode { get; set; } = string.Empty;

    /// <summary>授权访问的验证地址。 / Verification URL the user should open.</summary>
    public string VerificationUri { get; set; } = string.Empty;

    /// <summary>验证码剩余有效秒数。 / Remaining lifetime of the code in seconds.</summary>
    public int ExpiresIn { get; set; }

    /// <summary>建议的轮询间隔秒数。 / Recommended polling interval in seconds.</summary>
    public int Interval { get; set; }
}

/// <summary>
/// 设备授权轮询结果。
/// Polling result for an in-progress device authorization flow.
/// </summary>
public class DeviceAuthPollDto
{
    /// <summary>授权状态，例如 pending、approved、denied 或 expired。 / Authorization status such as pending, approved, denied, or expired.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>面向调用方的状态说明。 / Human-readable status message for the caller.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>服务端建议的新轮询间隔秒数。 / Optional polling interval override in seconds.</summary>
    public int? Interval { get; set; }
}
