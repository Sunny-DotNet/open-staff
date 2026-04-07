namespace OpenStaff.Application.Contracts.AgentRoles.Dtos;

public class ProviderSchemaDto
{
    public string ProviderType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<ProviderFieldDto> Fields { get; set; } = [];
}

public class ProviderFieldDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string FieldType { get; set; } = "text";
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public string? Placeholder { get; set; }
    public List<ProviderFieldOptionDto>? Options { get; set; }
}

public class ProviderFieldOptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
