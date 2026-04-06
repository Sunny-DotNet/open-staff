namespace OpenStaff.Vendor;

/// <summary>
/// Vendor 运行时配置 — 从数据库或前端传入的实际配置值
/// </summary>
public class VendorConfig
{
    /// <summary>配置键值对（key 对应 VendorConfigField.Key）</summary>
    public Dictionary<string, string?> Values { get; set; } = [];

    /// <summary>获取配置值</summary>
    public string? Get(string key) =>
        Values.TryGetValue(key, out var value) ? value : null;

    /// <summary>获取必填配置值（不存在则抛异常）</summary>
    public string GetRequired(string key) =>
        Get(key) ?? throw new InvalidOperationException($"Vendor config '{key}' is required but not set");
}
