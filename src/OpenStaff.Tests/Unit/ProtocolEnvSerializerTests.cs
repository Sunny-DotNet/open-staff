using OpenStaff.Infrastructure.Security;
using OpenStaff.Provider.Protocols;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class ProtocolEnvSerializerTests
{
    private static EncryptionService CreateEncryption()
        => new("test-encryption-key-for-unit-tests");

    // 测试用 Env 类
    private class TestEnv : ProtocolHasApiKeyEnvBase
    {
        public override string BaseUrl { get; set; } = "https://api.example.com";
        protected override string ApiKeyFromEnvDefault => "TEST_API_KEY";
        public string ExtraField { get; set; } = "extra";
    }

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

        // BaseUrl 和 ExtraField 应该明文
        Assert.Contains("api.openai.com", json);
        Assert.Contains("visible", json);
        // ApiKey 不应该明文出现
        Assert.DoesNotContain("sk-secret-key", json);
    }

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

    [Fact]
    public void Deserialize_WithoutDecrypt_ShouldReturnEncryptedValues()
    {
        var enc = CreateEncryption();
        var env = new TestEnv { ApiKey = "sk-secret-key" };

        var json = ProtocolEnvSerializer.Serialize(env, enc.Encrypt);
        var result = ProtocolEnvSerializer.Deserialize<TestEnv>(json, decrypt: null);

        Assert.NotNull(result);
        Assert.NotEqual("sk-secret-key", result!.ApiKey); // still encrypted
        Assert.Equal("https://api.example.com", result.BaseUrl); // plaintext preserved
    }

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
        // Case-insensitive lookup
        Assert.Equal("sk-test", dict!["ApiKey"]?.ToString());
        Assert.Equal("sk-test", dict["apiKey"]?.ToString());
    }

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

    [Fact]
    public void Deserialize_NullInput_ShouldReturnNull()
    {
        var result = ProtocolEnvSerializer.Deserialize<TestEnv>(null!);
        Assert.Null(result);

        result = ProtocolEnvSerializer.Deserialize<TestEnv>("");
        Assert.Null(result);
    }

    [Fact]
    public void DeserializeToDict_PlaintextFallback_ShouldWork()
    {
        // 如果 JSON 中的值是明文（如从 dev 环境），不应该尝试解密
        var env = new TestEnv { ApiKey = "plain-key" };
        var json = ProtocolEnvSerializer.Serialize(env); // no encryption

        var enc = CreateEncryption();
        // 用 decrypt 去读明文 — 明文不是有效的 Base64 加密，应该保持原值
        var dict = ProtocolEnvSerializer.DeserializeToDict(json, typeof(TestEnv), enc.Decrypt);

        Assert.NotNull(dict);
        Assert.Equal("plain-key", dict!["apiKey"]?.ToString());
    }
}
