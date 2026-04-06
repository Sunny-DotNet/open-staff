using System.Text.Json.Serialization;

namespace OpenStaff.Marketplace.Registry;

/// <summary>
/// Registry API 响应模型 — 对应 registry.modelcontextprotocol.io/v0/servers
/// </summary>
public class RegistryResponse
{
    [JsonPropertyName("servers")]
    public List<RegistryServerEntry> Servers { get; set; } = [];

    [JsonPropertyName("metadata")]
    public RegistryMetadata Metadata { get; set; } = new();
}

public class RegistryServerEntry
{
    [JsonPropertyName("server")]
    public RegistryServer Server { get; set; } = new();

    [JsonPropertyName("_meta")]
    public RegistryEntryMeta? Meta { get; set; }
}

public class RegistryServer
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("websiteUrl")]
    public string? WebsiteUrl { get; set; }

    [JsonPropertyName("repository")]
    public RegistryRepository? Repository { get; set; }

    [JsonPropertyName("remotes")]
    public List<RegistryRemote>? Remotes { get; set; }

    [JsonPropertyName("packages")]
    public List<RegistryPackage>? Packages { get; set; }
}

public class RegistryRepository
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string? Source { get; set; }
}

public class RegistryRemote
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class RegistryPackage
{
    [JsonPropertyName("registryType")]
    public string? RegistryType { get; set; }

    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("transport")]
    public RegistryTransport? Transport { get; set; }

    [JsonPropertyName("environmentVariables")]
    public List<RegistryEnvironmentVariable>? EnvironmentVariables { get; set; }
}

public class RegistryTransport
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public class RegistryEnvironmentVariable
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; }

    [JsonPropertyName("isSecret")]
    public bool IsSecret { get; set; }
}

public class RegistryPackageInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class RegistryEntryMeta
{
    [JsonPropertyName("io.modelcontextprotocol.registry/official")]
    public RegistryOfficialMeta? Official { get; set; }
}

public class RegistryOfficialMeta
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("isLatest")]
    public bool IsLatest { get; set; }

    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; set; }
}

public class RegistryMetadata
{
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
