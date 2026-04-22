namespace OpenStaff.Dtos;

/// <summary>
/// Vendor Agent 提供的模型信息。
/// Model metadata exposed by a vendor-backed agent provider.
/// </summary>
/// <param name="Id">模型标识。 / Model identifier.</param>
/// <param name="Name">模型显示名称。 / Display name of the model.</param>
/// <param name="Family">模型家族或系列。 / Model family or series.</param>
public record VendorModelDto(string Id, string Name, string? Family);
