namespace OpenStaff.Dtos;



public record struct ProviderInfo(
    string Key,
    string DisplayName,
    string Description,
    string? Logo=null);


public class ProviderAccountQueryInput
{
    /// <summary>协议类型键列表。 / List of provider protocol type keys to filter by.</summary>
    public List<string>? ProtocolTypes { get; set; }
    /// <summary>是否仅包含启用的账户。 / Whether to include only enabled accounts.</summary>
    public bool? IsEnabled { get; set; }
}
/// <summary>
/// 提供商账户摘要信息。
/// Summary information for a provider account.
/// </summary>
public class ProviderAccountDto
{
    /// <summary>账户唯一标识。 / Unique account identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>账户名称。 / Account name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>协议类型键。 / Provider protocol type key.</summary>
    public string ProtocolType { get; set; } = string.Empty;

    /// <summary>账户是否启用。 / Whether the account is enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>创建时间（UTC）。 / Creation time in UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>最后更新时间（UTC）。 / Last update time in UTC.</summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 创建提供商账户的输入参数。
/// Input used to create a provider account.
/// </summary>
public class CreateProviderAccountInput
{
    /// <summary>账户名称。 / Account name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>协议类型键。 / Provider protocol type key.</summary>
    public string ProtocolType { get; set; } = string.Empty;

    /// <summary>是否创建后立即启用。 / Whether the account should be enabled immediately.</summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 更新提供商账户的输入参数。
/// Input used to update a provider account.
/// </summary>
public class UpdateProviderAccountInput
{
    /// <summary>账户名称。 / Account name.</summary>
    public string? Name { get; set; }


    /// <summary>是否启用。 / Whether the account is enabled.</summary>
    public bool? IsEnabled { get; set; }
}

