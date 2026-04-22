namespace OpenStaff.Dtos;

/// <summary>
/// Vendor provider configuration snapshot used by the frontend to render and edit provider-level settings.
/// 供前端渲染和编辑 provider 级配置的 Vendor provider 配置快照。
/// </summary>
public class VendorProviderConfigurationDto
{
    /// <summary>Provider type key. / Provider 类型键。</summary>
    public string ProviderType { get; set; } = string.Empty;

    /// <summary>Display name shown by the UI. / 前端显示名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional avatar resource. / 可选头像资源。</summary>
    public string? AvatarDataUri { get; set; }

    /// <summary>Configuration property definitions. / 配置字段定义。</summary>
    public List<VendorProviderConfigurationPropertyDto> Properties { get; set; } = [];

    /// <summary>Current configuration values. / 当前配置值。</summary>
    public Dictionary<string, object?> Configuration { get; set; } = [];
}

/// <summary>
/// Single provider-level configuration field definition.
/// 单个 provider 级配置字段定义。
/// </summary>
public class VendorProviderConfigurationPropertyDto
{
    /// <summary>Configuration key. / 配置键名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Frontend field type. / 前端字段类型。</summary>
    public string FieldType { get; set; } = string.Empty;

    /// <summary>Default value. / 默认值。</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Whether the field is required. / 是否必填。</summary>
    public bool Required { get; set; }
}

/// <summary>
/// Update payload for provider-level configuration.
/// Provider 级配置更新载荷。
/// </summary>
public class UpdateVendorProviderConfigurationInput
{
    /// <summary>Configuration values to persist. / 需要持久化的配置值。</summary>
    public Dictionary<string, object?> Configuration { get; set; } = [];
}
