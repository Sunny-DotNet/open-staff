using System.Text.Json.Serialization;

namespace OpenStaff.Core.Agents;

/// <summary>
/// 角色配置 / Role configuration (deserialized from embedded JSON)
/// </summary>
public class RoleConfig
{
    /// <summary>角色类型标识 / Unique role type identifier.</summary>
    [JsonPropertyName("roleType")]
    public string RoleType { get; set; } = string.Empty;

    /// <summary>角色展示名称 / Display name of the role.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>角色描述 / Optional role description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>是否为内置角色 / Whether the role is built in.</summary>
    [JsonPropertyName("isBuiltin")]
    public bool IsBuiltin { get; set; }

    /// <summary>默认模型名称 / Default model name.</summary>
    [JsonPropertyName("modelName")]
    public string? ModelName { get; set; }

    /// <summary>模型参数覆盖项 / Optional model parameter overrides.</summary>
    [JsonPropertyName("modelParameters")]
    public ModelParameters? ModelParameters { get; set; }

    /// <summary>路由配置 / Routing configuration.</summary>
    [JsonPropertyName("routing")]
    public RoutingConfig? Routing { get; set; }

    /// <summary>
    /// 创建浅拷贝以便覆盖顶层属性 / Create a shallow copy so top-level properties can be overridden without mutating the original.
    /// </summary>
    /// <remarks>
    /// 引用类型成员（例如集合和嵌套路由配置）仍会共享同一实例，因此修改副本中的这些对象会影响原对象。
    /// Reference-type members such as collections and nested routing objects remain shared, so mutating them on the clone also affects the original instance.
    /// </remarks>
    public RoleConfig Clone() => (RoleConfig)MemberwiseClone();
}

/// <summary>
/// 模型参数覆盖项 / Model parameter overrides for a role.
/// </summary>
public class ModelParameters
{
    /// <summary>采样温度 / Sampling temperature.</summary>
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;

    /// <summary>最大输出 Token 数 / Maximum output token count.</summary>
    [JsonPropertyName("maxTokens")]
    public int MaxTokens { get; set; } = 4096;
}

/// <summary>
/// 角色路由配置 / Routing rules for role handoff.
/// </summary>
public class RoutingConfig
{
    /// <summary>标记到下一角色的映射 / Mapping from routing markers to next role types.</summary>
    [JsonPropertyName("markers")]
    public Dictionary<string, string> Markers { get; set; } = new();

    /// <summary>默认下一角色 / Default next role when no marker matches.</summary>
    [JsonPropertyName("defaultNext")]
    public string? DefaultNext { get; set; }
}
