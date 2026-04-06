namespace OpenStaff.Application.Contracts.Providers.Dtos;

public class ProviderAccountDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProtocolType { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ProviderAccountDetailDto : ProviderAccountDto
{
    public Dictionary<string, object?>? EnvConfig { get; set; }
}

public class CreateProviderAccountInput
{
    public string Name { get; set; } = string.Empty;
    public string ProtocolType { get; set; } = string.Empty;
    public Dictionary<string, object>? EnvConfig { get; set; }
    public bool IsEnabled { get; set; }
}

public class UpdateProviderAccountInput
{
    public string? Name { get; set; }
    public Dictionary<string, object>? EnvConfig { get; set; }
    public bool? IsEnabled { get; set; }
}

public class ProviderModelDto
{
    public string Id { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public string Protocols { get; set; } = string.Empty;
}
