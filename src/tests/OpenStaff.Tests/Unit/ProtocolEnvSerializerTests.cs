using OpenStaff.Infrastructure.Security;
using OpenStaff.Provider.Protocols;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class ProtocolEnvSerializerTests
{
    /// <summary>
    /// zh-CN: 创建固定密钥的加密服务，让序列化测试能够稳定验证加解密分支而不依赖外部配置。
    /// en: Creates an encryption service with a fixed key so the serialization tests can verify encrypt/decrypt paths deterministically without external configuration.
    /// </summary>
    private static EncryptionService CreateEncryption()
        => new("test-encryption-key-for-unit-tests");

    /// <summary>
    /// zh-CN: 提供最小的协议环境类型，用来覆盖默认环境变量、受保护字段和普通字段的序列化契约。
    /// en: Provides a minimal protocol environment type that exercises default env metadata, protected fields, and ordinary fields in the serialization contract.
    /// </summary>
    private class TestEnv : ProtocolApiKeyEnvironmentBase
    {
        /// <summary>
        /// zh-CN: 提供稳定的默认地址，便于区分哪些字段应始终保持明文。
        /// en: Supplies a stable default endpoint so the tests can distinguish fields that should always remain plaintext.
        /// </summary>
        public override string BaseUrl { get; set; } = "https://api.example.com";

        /// <summary>
        /// zh-CN: 使用测试专用环境变量名，证明默认 API Key 元数据也会随类型定义一起参与序列化。
        /// en: Uses a test-specific environment variable name to show that default API-key metadata travels with the type definition during serialization.
        /// </summary>
        protected override string ApiKeyFromEnvDefault => "TEST_API_KEY";
        public string ExtraField { get; set; } = "extra";
    }

    /// <summary>
    /// zh-CN: 验证启用加密时只有被标记的敏感字段会被加密，普通配置值仍保持可读。
    /// en: Verifies that enabling encryption only encrypts marked sensitive fields while ordinary configuration values remain readable.
    /// </summary>
    [Fact]
    public void Serialize_WithEncrypt_ShouldEncryptOnlyMarkedFields()
    {
        var enc = CreateEncryption();
        var env = new TestEnv
        {
            ApiKey = "sk-secret-key",
            BaseUrl = "https://api.openai.com",
            ExtraField = "visible"
        };

        var json = ProtocolEnvSerializer.Serialize(env, enc.Encrypt);

        // zh-CN: BaseUrl 与 ExtraField 代表普通配置字段，应保持明文以便调试和回显。
        // en: BaseUrl and ExtraField represent ordinary configuration fields and should remain plaintext for debugging and display.
        Assert.Contains("api.openai.com", json);
        Assert.Contains("visible", json);
        // zh-CN: ApiKey 属于受保护字段，序列化后不应再以明文暴露。
        // en: ApiKey is a protected field and should no longer appear in plaintext after serialization.
        Assert.DoesNotContain("sk-secret-key", json);
    }

    /// <summary>
    /// zh-CN: 验证未提供加密委托时，序列化结果保持开发态明文，方便本地配置排查。
    /// en: Verifies that serialization stays fully plaintext when no encrypt delegate is supplied, which keeps local-development configuration easy to inspect.
    /// </summary>
    [Fact]
    public void Serialize_WithoutEncrypt_ShouldKeepAllPlaintext()
    {
        var env = new TestEnv
        {
            ApiKey = "sk-secret-key",
            BaseUrl = "https://api.openai.com",
        };

        var json = ProtocolEnvSerializer.Serialize(env, encrypt: null);

        Assert.Contains("sk-secret-key", json);
        Assert.Contains("api.openai.com", json);
    }

    /// <summary>
    /// zh-CN: 验证反序列化在提供解密委托时，会恢复敏感字段同时保留普通字段原值。
    /// en: Verifies that deserialization restores sensitive fields when a decrypt delegate is provided while preserving the original values of ordinary fields.
    /// </summary>
    [Fact]
    public void Deserialize_ShouldDecryptEncryptedFields()
    {
        var enc = CreateEncryption();
        var env = new TestEnv
        {
            ApiKey = "sk-secret-key",
            BaseUrl = "https://api.openai.com",
            ExtraField = "hello"
        };

        var json = ProtocolEnvSerializer.Serialize(env, enc.Encrypt);
        var result = ProtocolEnvSerializer.Deserialize<TestEnv>(json, enc.Decrypt);

        Assert.NotNull(result);
        Assert.Equal("sk-secret-key", result!.ApiKey);
        Assert.Equal("https://api.openai.com", result.BaseUrl);
        Assert.Equal("hello", result.ExtraField);
    }

    /// <summary>
    /// zh-CN: 验证未提供解密器时，受保护字段保持密文，而未加密字段仍按明文返回。
    /// en: Verifies that protected fields remain encrypted without a decryptor while unencrypted fields still come back as plaintext.
    /// </summary>
    [Fact]
    public void Deserialize_WithoutDecrypt_ShouldReturnEncryptedValues()
    {
        var enc = CreateEncryption();
        var env = new TestEnv { ApiKey = "sk-secret-key" };

        var json = ProtocolEnvSerializer.Serialize(env, enc.Encrypt);
        var result = ProtocolEnvSerializer.Deserialize<TestEnv>(json, decrypt: null);

        Assert.NotNull(result);
        // zh-CN: 未传入解密器时，ApiKey 应继续保留密文，避免意外解密。
        // en: Without a decryptor, ApiKey should stay encrypted so the serializer never decrypts by accident.
        Assert.NotEqual("sk-secret-key", result!.ApiKey);
        // zh-CN: BaseUrl 从未加密，因此应完整保留默认明文值。
        // en: BaseUrl is never encrypted, so its default plaintext value should be preserved intact.
        Assert.Equal("https://api.example.com", result.BaseUrl);
    }

    /// <summary>
    /// zh-CN: 验证字典反序列化既会解密敏感字段，也会提供大小写不敏感的键访问体验。
    /// en: Verifies that dictionary deserialization both decrypts sensitive fields and exposes case-insensitive key lookup.
    /// </summary>
    [Fact]
    public void DeserializeToDict_ShouldDecryptAndBeCaseInsensitive()
    {
        var enc = CreateEncryption();
        var env = new TestEnv
        {
            ApiKey = "sk-test",
            BaseUrl = "https://example.com"
        };

        var json = ProtocolEnvSerializer.Serialize(env, enc.Encrypt);
        var dict = ProtocolEnvSerializer.DeserializeToDict(json, typeof(TestEnv), enc.Decrypt);

        Assert.NotNull(dict);
        // zh-CN: 字典键访问应忽略大小写，便于不同调用方按各自命名习惯读取。
        // en: Dictionary lookup should ignore casing so different callers can read values using their preferred naming style.
        Assert.Equal("sk-test", dict!["ApiKey"]?.ToString());
        Assert.Equal("sk-test", dict["apiKey"]?.ToString());
    }

    /// <summary>
    /// zh-CN: 验证开发模式的无加密往返不会丢失字段值，确保本地调试链路与生产逻辑兼容。
    /// en: Verifies that a no-encryption round trip preserves values in development mode so local debugging stays compatible with production logic.
    /// </summary>
    [Fact]
    public void RoundTrip_DevMode_NoEncryption()
    {
        var env = new TestEnv
        {
            ApiKey = "sk-dev-key",
            BaseUrl = "http://localhost:8080",
            ExtraField = "dev-extra"
        };

        var json = ProtocolEnvSerializer.Serialize(env);
        var result = ProtocolEnvSerializer.Deserialize<TestEnv>(json);

        Assert.NotNull(result);
        Assert.Equal("sk-dev-key", result!.ApiKey);
        Assert.Equal("http://localhost:8080", result.BaseUrl);
        Assert.Equal("dev-extra", result.ExtraField);
    }

    /// <summary>
    /// zh-CN: 验证空字符串 API Key 不会被误当作有效密文处理，避免生成无意义的密文占位。
    /// en: Verifies that an empty API key is not treated as a meaningful ciphertext candidate, avoiding useless encrypted placeholders.
    /// </summary>
    [Fact]
    public void Serialize_EmptyApiKey_ShouldNotEncrypt()
    {
        var enc = CreateEncryption();
        var env = new TestEnv { ApiKey = "" };

        var json = ProtocolEnvSerializer.Serialize(env, enc.Encrypt);
        var result = ProtocolEnvSerializer.Deserialize<TestEnv>(json, enc.Decrypt);

        Assert.NotNull(result);
        Assert.True(string.IsNullOrEmpty(result!.ApiKey));
    }

    /// <summary>
    /// zh-CN: 验证空输入会被安全地识别为无配置，而不是抛出解析异常。
    /// en: Verifies that null or empty input is safely treated as missing configuration instead of throwing a parsing exception.
    /// </summary>
    [Fact]
    public void Deserialize_NullInput_ShouldReturnNull()
    {
        var result = ProtocolEnvSerializer.Deserialize<TestEnv>(null!);
        Assert.Null(result);

        result = ProtocolEnvSerializer.Deserialize<TestEnv>("");
        Assert.Null(result);
    }

    /// <summary>
    /// zh-CN: 验证明文 JSON 在传入解密器时仍能回退为原值，兼容开发环境或历史未加密数据。
    /// en: Verifies that plaintext JSON still falls back to the original value even when a decryptor is supplied, preserving compatibility with development or legacy unencrypted data.
    /// </summary>
    [Fact]
    public void DeserializeToDict_PlaintextFallback_ShouldWork()
    {
        // zh-CN: 如果 JSON 中保存的是明文（例如来自 dev 环境），读取流程不应强制要求其可被解密。
        // en: If the JSON stores plaintext values, such as from a dev environment, the read path should not require them to be decryptable.
        var env = new TestEnv { ApiKey = "plain-key" };
        var json = ProtocolEnvSerializer.Serialize(env);

        var enc = CreateEncryption();
        // zh-CN: 即使用了解密委托去读明文，无法解密的值也应原样返回而不是报错。
        // en: Even when a decrypt delegate is provided, plaintext that cannot be decrypted should be returned as-is instead of failing.
        var dict = ProtocolEnvSerializer.DeserializeToDict(json, typeof(TestEnv), enc.Decrypt);

        Assert.NotNull(dict);
        Assert.Equal("plain-key", dict!["apiKey"]?.ToString());
    }
}
