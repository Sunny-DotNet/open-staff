namespace OpenStaff.Application.Contracts.AgentRoles.Dtos;

public class VendorSchemaDto
{
    public string VendorType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<VendorFieldDto> Fields { get; set; } = [];
}

public class VendorFieldDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string FieldType { get; set; } = "text";
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public string? Placeholder { get; set; }
    public List<VendorFieldOptionDto>? Options { get; set; }
}

public class VendorFieldOptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
