using System.Text.Json.Serialization;

namespace OpenStaff.Core.Agents;

/// <summary>
/// 角色配置 / Role configuration (deserialized from embedded JSON)
/// </summary>
public class RoleConfig
{
    [JsonPropertyName("roleType")]
    public string RoleType { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isBuiltin")]
    public bool IsBuiltin { get; set; }

    [JsonPropertyName("systemPrompt")]
    public string SystemPrompt { get; set; } = string.Empty;

    [JsonPropertyName("modelName")]
    public string? ModelName { get; set; }

    [JsonPropertyName("modelParameters")]
    public ModelParameters? ModelParameters { get; set; }

    [JsonPropertyName("tools")]
    public List<string> Tools { get; set; } = new();

    [JsonPropertyName("routing")]
    public RoutingConfig? Routing { get; set; }

    /// <summary>浅拷贝，用于在不修改原始配置的情况下覆盖部分属性</summary>
    public RoleConfig Clone() => (RoleConfig)MemberwiseClone();
}

public class ModelParameters
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;

    [JsonPropertyName("maxTokens")]
    public int MaxTokens { get; set; } = 4096;
}

public class RoutingConfig
{
    [JsonPropertyName("markers")]
    public Dictionary<string, string> Markers { get; set; } = new();

    [JsonPropertyName("defaultNext")]
    public string? DefaultNext { get; set; }
}
