using System.Text.Json.Serialization;

namespace OpenStaff.Marketplace.Registry;

/// <summary>
/// Registry 列表接口响应模型。
/// Response model returned by the registry listing API.
/// </summary>
public class RegistryResponse
{
    /// <summary>
    /// 返回的服务条目列表。
    /// Returned server entries.
    /// </summary>
    [JsonPropertyName("servers")]
    public List<RegistryServerEntry> Servers { get; set; } = [];

    /// <summary>
    /// 返回的分页元数据。
    /// Returned pagination metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public RegistryMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Registry 服务条目包装对象。
/// Wrapper object for a registry server entry.
/// </summary>
public class RegistryServerEntry
{
    /// <summary>
    /// 服务主体信息。
    /// Core server payload.
    /// </summary>
    [JsonPropertyName("server")]
    public RegistryServer Server { get; set; } = new();

    /// <summary>
    /// 附加元数据。
    /// Additional metadata.
    /// </summary>
    [JsonPropertyName("_meta")]
    public RegistryEntryMeta? Meta { get; set; }
}

/// <summary>
/// Registry 中的服务主体描述。
/// Core description of a server stored in the registry.
/// </summary>
public class RegistryServer
{
    /// <summary>
    /// 服务名称。
    /// Server name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 服务描述。
    /// Server description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// 服务版本。
    /// Server version.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// 官网地址。
    /// Website URL.
    /// </summary>
    [JsonPropertyName("websiteUrl")]
    public string? WebsiteUrl { get; set; }

    /// <summary>
    /// 仓库信息。
    /// Repository information.
    /// </summary>
    [JsonPropertyName("repository")]
    public RegistryRepository? Repository { get; set; }

    /// <summary>
    /// 远程传输端点。
    /// Remote transport endpoints.
    /// </summary>
    [JsonPropertyName("remotes")]
    public List<RegistryRemote>? Remotes { get; set; }

    /// <summary>
    /// 可安装包信息。
    /// Installable package descriptors.
    /// </summary>
    [JsonPropertyName("packages")]
    public List<RegistryPackage>? Packages { get; set; }
}

/// <summary>
/// Registry 仓库信息。
/// Repository information returned by the registry.
/// </summary>
public class RegistryRepository
{
    /// <summary>
    /// 仓库地址。
    /// Repository URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 仓库来源类型。
    /// Repository source type.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }
}

/// <summary>
/// Registry 远程端点描述。
/// Registry remote endpoint description.
/// </summary>
public class RegistryRemote
{
    /// <summary>
    /// 远程端点类型。
    /// Remote endpoint type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 远程端点地址。
    /// Remote endpoint URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Registry 安装包信息。
/// Registry installable package information.
/// </summary>
public class RegistryPackage
{
    /// <summary>
    /// 包注册表类型，例如 npm 或 pypi。
    /// Package registry type such as npm or pypi.
    /// </summary>
    [JsonPropertyName("registryType")]
    public string? RegistryType { get; set; }

    /// <summary>
    /// 包标识。
    /// Package identifier.
    /// </summary>
    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    /// <summary>
    /// 包版本。
    /// Package version.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// 包传输配置。
    /// Package transport configuration.
    /// </summary>
    [JsonPropertyName("transport")]
    public RegistryTransport? Transport { get; set; }

    /// <summary>
    /// 所需环境变量列表。
    /// Required environment variables.
    /// </summary>
    [JsonPropertyName("environmentVariables")]
    public List<RegistryEnvironmentVariable>? EnvironmentVariables { get; set; }
}

/// <summary>
/// Registry 包传输配置。
/// Registry package transport configuration.
/// </summary>
public class RegistryTransport
{
    /// <summary>
    /// 传输类型。
    /// Transport type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Registry 包环境变量描述。
/// Registry package environment variable description.
/// </summary>
public class RegistryEnvironmentVariable
{
    /// <summary>
    /// 环境变量名称。
    /// Environment variable name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 环境变量说明。
    /// Environment variable description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// 是否为必填变量。
    /// Indicates whether the variable is required.
    /// </summary>
    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; }

    /// <summary>
    /// 是否为敏感变量。
    /// Indicates whether the variable is sensitive.
    /// </summary>
    [JsonPropertyName("isSecret")]
    public bool IsSecret { get; set; }
}

/// <summary>
/// Registry 包信息的精简视图。
/// Compact view of registry package information.
/// </summary>
public class RegistryPackageInfo
{
    /// <summary>
    /// 包名称。
    /// Package name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Registry 条目扩展元数据。
/// Extended metadata attached to a registry entry.
/// </summary>
public class RegistryEntryMeta
{
    /// <summary>
    /// 官方发布元数据。
    /// Official release metadata.
    /// </summary>
    [JsonPropertyName("io.modelcontextprotocol.registry/official")]
    public RegistryOfficialMeta? Official { get; set; }
}

/// <summary>
/// 官方发布状态元数据。
/// Official release status metadata.
/// </summary>
public class RegistryOfficialMeta
{
    /// <summary>
    /// 发布状态。
    /// Release status.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// 是否为最新版本。
    /// Indicates whether the entry is the latest version.
    /// </summary>
    [JsonPropertyName("isLatest")]
    public bool IsLatest { get; set; }

    /// <summary>
    /// 发布时间。
    /// Publication time.
    /// </summary>
    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; set; }
}

/// <summary>
/// Registry 响应分页元数据。
/// Pagination metadata returned by the registry.
/// </summary>
public class RegistryMetadata
{
    /// <summary>
    /// 下一页游标。
    /// Cursor for the next page.
    /// </summary>
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    /// <summary>
    /// 当前返回数量。
    /// Count returned by the current response.
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
