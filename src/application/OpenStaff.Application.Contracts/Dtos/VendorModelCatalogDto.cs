namespace OpenStaff.Dtos;

/// <summary>
/// Vendor model catalog snapshot exposed to the frontend.
/// 提供给前端的 Vendor 模型目录状态快照。
/// </summary>
public class VendorModelCatalogDto
{
    /// <summary>Provider type key. / Provider 类型键。</summary>
    public string ProviderType { get; set; } = string.Empty;

    /// <summary>Catalog status. / 目录状态。</summary>
    public string Status { get; set; } = "ready";

    /// <summary>Optional human-readable message. / 可选提示信息。</summary>
    public string? Message { get; set; }

    /// <summary>Missing provider-level configuration fields. / 缺失的 provider 级配置字段。</summary>
    public List<string> MissingConfigurationFields { get; set; } = [];

    /// <summary>Discovered models. / 已发现的模型列表。</summary>
    public List<VendorModelDto> Models { get; set; } = [];
}
