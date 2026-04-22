namespace OpenStaff.Dtos;

/// <summary>
/// 提供商公开的模型摘要。
/// Summary of a model exposed by a provider account.
/// </summary>
public class ProviderModelDto
{
    /// <summary>模型标识。 / Model identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>模型所属厂商。 / Vendor that owns the model.</summary>
    public string Vendor { get; set; } = string.Empty;

    /// <summary>支持的协议列表文本。 / Text representation of the supported protocols.</summary>
    public string Protocols { get; set; } = string.Empty;
}
