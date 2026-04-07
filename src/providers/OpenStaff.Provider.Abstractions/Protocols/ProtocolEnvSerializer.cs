using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenStaff.Provider.Protocols;

/// <summary>
/// ProtocolEnv 序列化器 — 字段级加密
/// [Encrypted] 标记的属性值单独加密，其他属性明文存储
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
    /// 序列化 ProtocolEnv 为 JSON，[Encrypted] 属性值加密
    /// </summary>
    /// <param name="env">ProtocolEnv 实例</param>
    /// <param name="encrypt">加密函数（传 null 则不加密，如 dev 环境）</param>
    public static string Serialize<T>(T env, Func<string, string>? encrypt = null) where T : ProtocolEnvBase
    {
        var dict = new Dictionary<string, object?>();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.DeclaringType != typeof(object));

        foreach (var prop in properties)
        {
            var value = prop.GetValue(env);
            var key = JsonNamingPolicy.CamelCase.ConvertName(prop.Name);

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
    /// 从 JSON 反序列化 ProtocolEnv，[Encrypted] 属性值解密
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="decrypt">解密函数（传 null 则不解密，如 dev 环境）</param>
    public static T? Deserialize<T>(string json, Func<string, string>? decrypt = null) where T : ProtocolEnvBase
    {
        if (string.IsNullOrEmpty(json)) return null;

        var env = JsonSerializer.Deserialize<T>(json, JsonOptions);
        if (env == null) return null;

        if (decrypt == null) return env;

        // 解密 [Encrypted] 标记的属性
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
                    // 解密失败保持原值（可能是明文，如从 dev 切到 prod）
                }
            }
        }

        return env;
    }

    /// <summary>
    /// 从 JSON 反序列化为 Dictionary，[Encrypted] 属性值解密
    /// </summary>
    public static Dictionary<string, object?>? DeserializeToDict(
        string json, Type envType, Func<string, string>? decrypt = null)
    {
        if (string.IsNullOrEmpty(json)) return null;

        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions);
        if (dict == null) return null;

        // 返回大小写不敏感的字典
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
                    catch { /* 保持原值 */ }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 判断一个字符串值是否是 AES 加密过的（Base64 且长度合理）
    /// </summary>
    private static bool IsEncrypted(string value)
    {
        // AES-256 加密后的值是 Base64 编码，IV(16字节) + 密文，最小 32 字节 → Base64 至少 44 字符
        if (value.Length < 44) return false;
        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length >= 32; // IV(16) + 至少1 block(16)
        }
        catch
        {
            return false;
        }
    }
}
