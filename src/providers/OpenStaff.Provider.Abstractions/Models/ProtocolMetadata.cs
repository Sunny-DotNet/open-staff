namespace OpenStaff.Provider.Models;

/// <summary>
/// 协议元数据 — 描述一个已注册的 Protocol 类型及其配置 schema
/// </summary>
public record ProtocolMetadata(
    string ProtocolName,
    bool IsVendor,
    string ProtocolClassName,
    IReadOnlyList<ProtocolEnvField> EnvSchema);

/// <summary>
/// 协议环境配置字段描述
/// </summary>
public record ProtocolEnvField(
    string Name,
    string FieldType,  // "string" | "secret" | "bool" | "number"
    string DefaultValue);
