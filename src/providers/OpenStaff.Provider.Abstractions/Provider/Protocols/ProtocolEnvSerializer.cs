using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenStaff.Provider.Protocols;

/// <summary>
/// Protocol 环境配置序列化器，对标记为加密字段的值执行单独保护。
/// Serializer for protocol environment settings that applies dedicated protection to encrypted fields.
/// </summary>
public static class ProtocolEnvSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 将协议环境配置序列化为 JSON。
    /// Serializes protocol environment settings into JSON.
    /// </summary>
    /// <typeparam name="T">
    /// 协议环境配置类型。
    /// Protocol environment type.
    /// </typeparam>
    /// <param name="env">
    /// 要序列化的协议环境配置。
    /// Protocol environment settings to serialize.
    /// </param>
    /// <param name="encrypt">
    /// 加密函数；传入 <see langword="null" /> 时不加密字段值。
    /// Encryption delegate; pass <see langword="null" /> to keep field values in plaintext.
    /// </param>
    /// <returns>
    /// 序列化后的 JSON 字符串。
    /// Serialized JSON string.
    /// </returns>
    public static string Serialize<T>(T env, Func<string, string>? encrypt = null) where T : ProtocolEnvBase
    {
        var dict = new Dictionary<string, object?>();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.DeclaringType != typeof(object));

        foreach (var prop in properties)
        {
            var value = prop.GetValue(env);
            var key = JsonNamingPolicy.CamelCase.ConvertName(prop.Name);

            // zh-CN: 仅对显式标记为 [Encrypted] 的字符串字段做加密，其他字段保持可读以便调试和兼容旧配置。
            // en: Only string properties explicitly marked with [Encrypted] are encrypted; other fields stay readable for debugging and backward compatibility.
            if (value is string strValue && !string.IsNullOrEmpty(strValue)
                && prop.GetCustomAttribute<EncryptedAttribute>() != null
                && encrypt != null)
            {
                dict[key] = encrypt(strValue);
            }
            else
            {
                dict[key] = value;
            }
        }

        return JsonSerializer.Serialize(dict, JsonOptions);
    }

    /// <summary>
    /// 从 JSON 反序列化协议环境配置。
    /// Deserializes protocol environment settings from JSON.
    /// </summary>
    /// <typeparam name="T">
    /// 协议环境配置类型。
    /// Protocol environment type.
    /// </typeparam>
    /// <param name="json">
    /// 协议环境配置 JSON。
    /// Protocol environment JSON.
    /// </param>
    /// <param name="decrypt">
    /// 解密函数；传入 <see langword="null" /> 时保留原始值。
    /// Decryption delegate; pass <see langword="null" /> to keep the raw stored values.
    /// </param>
    /// <returns>
    /// 反序列化后的环境配置实例；输入为空时返回 <see langword="null" />。
    /// Deserialized environment settings instance, or <see langword="null" /> when the input is empty.
    /// </returns>
    public static T? Deserialize<T>(string json, Func<string, string>? decrypt = null) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;

        var env = JsonSerializer.Deserialize<T>(json, JsonOptions);
        if (env == null) return null;

        if (decrypt == null) return env;

        // zh-CN: 仅尝试解密被 [Encrypted] 标记且看起来像密文的字符串，避免误伤开发环境中的明文配置。
        // en: Only decrypt [Encrypted] string properties that look like ciphertext so plaintext development settings are left untouched.
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<EncryptedAttribute>() != null);

        foreach (var prop in properties)
        {
            if (prop.PropertyType != typeof(string)) continue;

            var value = (string?)prop.GetValue(env);
            if (!string.IsNullOrEmpty(value) && IsEncrypted(value))
            {
                try
                {
                    prop.SetValue(env, decrypt(value));
                }
                catch
                {
                    // zh-CN: 解密失败时保留原值，兼容明文历史数据或不同环境间的迁移场景。
                    // en: Keep the original value on decryption failure to remain compatible with legacy plaintext data or cross-environment migrations.
                }
            }
        }

        return env;
    }

    /// <summary>
    /// 从 JSON 反序列化为大小写不敏感的字典。
    /// Deserializes JSON into a case-insensitive dictionary.
    /// </summary>
    /// <param name="json">
    /// 协议环境配置 JSON。
    /// Protocol environment JSON.
    /// </param>
    /// <param name="envType">
    /// 协议环境配置类型。
    /// Protocol environment type.
    /// </param>
    /// <param name="decrypt">
    /// 解密函数；传入 <see langword="null" /> 时保留原始值。
    /// Decryption delegate; pass <see langword="null" /> to keep the raw stored values.
    /// </param>
    /// <returns>
    /// 大小写不敏感的键值字典；输入为空时返回 <see langword="null" />。
    /// Case-insensitive key-value dictionary, or <see langword="null" /> when the input is empty.
    /// </returns>
    public static Dictionary<string, object?>? DeserializeToDict(
        string json, Type envType, Func<string, string>? decrypt = null)
    {
        if (string.IsNullOrEmpty(json)) return null;

        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions);
        if (dict == null) return null;

        var result = new Dictionary<string, object?>(dict, StringComparer.OrdinalIgnoreCase);
        if (decrypt == null) return result;

        var encryptedKeys = envType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<EncryptedAttribute>() != null)
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in encryptedKeys)
        {
            if (result.TryGetValue(key, out var val) && val is JsonElement je
                && je.ValueKind == JsonValueKind.String)
            {
                var strVal = je.GetString();
                if (!string.IsNullOrEmpty(strVal) && IsEncrypted(strVal))
                {
                    try
                    {
                        result[key] = decrypt(strVal);
                    }
                    catch
                    {
                        // zh-CN: 字典模式同样对历史明文配置做容错处理，失败时继续返回原始字符串。
                        // en: Dictionary mode also tolerates legacy plaintext settings by returning the original string when decryption fails.
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 通过轻量启发式判断字符串是否像已加密密文，避免对明文配置误做解密尝试。
    /// Uses a lightweight heuristic to decide whether a string resembles encrypted ciphertext so plaintext settings are not decrypted accidentally.
    /// </summary>
    /// <param name="value">
    /// 待检测的字符串值。
    /// String value to inspect.
    /// </param>
    /// <returns>
    /// 当值看起来像受保护密文时返回 <see langword="true" />；否则返回 <see langword="false" />。
    /// <see langword="true" /> when the value looks like protected ciphertext; otherwise <see langword="false" />.
    /// </returns>
    private static bool IsEncrypted(string value)
    {
        // zh-CN: 这里用“看起来像 Base64 且长度足够”的启发式判断密文，避免把任意字符串都交给解密器。
        // en: This heuristic checks whether the value looks like Base64 with a plausible ciphertext length so arbitrary strings are not passed to the decryptor.
        if (value.Length < 44) return false;

        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length >= 32;
        }
        catch
        {
            return false;
        }
    }
}
