namespace OpenStaff.Entities;

/// <summary>
/// 全局设置 / Global setting
/// </summary>
public class GlobalSetting:EntityBase<Guid>
{
    /// <summary>设置键 / Setting key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>设置值（通常为 JSON） / Setting value, typically stored as JSON.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>设置分类，如 general、model 或 locale / Setting category such as general, model, or locale.</summary>
    public string? Category { get; set; }

    /// <summary>设置说明 / Optional setting description.</summary>
    public string? Description { get; set; }

    /// <summary>创建时间（UTC） / Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间（UTC） / Last update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
