namespace OpenStaff.Core.Models;

/// <summary>
/// 全局设置 / Global setting
/// </summary>
public class GlobalSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty; // JSON
    public string? Category { get; set; } // general/model/locale
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
