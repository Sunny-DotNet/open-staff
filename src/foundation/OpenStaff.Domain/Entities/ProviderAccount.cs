namespace OpenStaff.Entities;

/// <summary>
/// 供应商账户 — 一个协议类型可以创建多个账户
/// </summary>
public class ProviderAccount:EntityBase<Guid>,IMustHaveCreatedAt,IMayHaveUpdatedAt
{

    /// <summary>账户显示名称 / Display name of the account.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>协议类型键 / Protocol key, typically matching <c>IProtocol.ProtocolKey</c>.</summary>
    public string ProtocolType { get; set; } = string.Empty;

    /// <summary>是否启用 / Whether the account is enabled for use.</summary>
    public bool IsEnabled { get; set; } = false;

    /// <inheritdoc />
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <inheritdoc />
    public DateTime? UpdatedAt { get; set; } 
}
