using System.Text.Json.Serialization;

namespace OpenStaff.Provider.Models;

/// <summary>
/// 协议元数据，描述一个已注册协议及其环境配置架构。
/// Protocol metadata describing a registered protocol and its environment configuration schema.
/// </summary>
/// <param name="ProtocolKey">
/// 协议键。
/// Protocol lookup key.
/// </param>
/// <param name="ProtocolName">
/// 协议显示名称。
/// Human-readable protocol name.
/// </param>
/// <param name="Logo">
/// 协议图标标识。
/// Protocol logo identifier.
/// </param>
/// <param name="IsVendor">
/// 指示该协议是否直接代表供应商。
/// Indicates whether the protocol maps directly to a vendor.
/// </param>
/// <param name="ProtocolClassName">
/// 协议类型名称。
/// Protocol CLR type name.
/// </param>
/// <param name="EnvSchema">
/// 协议环境变量架构。
/// Environment schema exposed for configuring the protocol.
/// </param>
public record ProtocolMetadata(
    [property: JsonPropertyName("providerKey")] string ProtocolKey,
    [property: JsonPropertyName("providerName")] string ProtocolName,
    string Logo,
    bool IsVendor,
    string ProtocolClassName,
    IReadOnlyList<ProtocolEnvField> EnvSchema);

/// <summary>
/// 协议环境配置字段描述。
/// Describes a single environment field for protocol configuration.
/// </summary>
/// <param name="Name">
/// 字段名称。
/// Field name.
/// </param>
/// <param name="FieldType">
/// 字段类型，例如 string、secret、bool 或 number。
/// Field type such as string, secret, bool, or number.
/// </param>
/// <param name="DefaultValue">
/// 字段默认值。
/// Default value for the field.
/// </param>
public record ProtocolEnvField(
    string Name,
    string FieldType,
    string DefaultValue);
