using OpenStaff.Infrastructure.Security;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class EncryptionServiceTests
{
    /// <summary>
    /// zh-CN: 创建使用固定测试密钥的加密服务，确保各测试关注加解密行为而不是密钥来源。
    /// en: Creates an encryption service with a fixed test key so the tests focus on encryption behavior rather than key sourcing.
    /// </summary>
    private static EncryptionService CreateService()
    {
        return new EncryptionService("test-encryption-key-for-unit-tests");
    }

    /// <summary>
    /// zh-CN: 验证普通文本加密后会生成非空密文，确保调用方拿到可持久化的输出。
    /// en: Verifies that encrypting regular text produces a non-empty ciphertext so callers receive a storable result.
    /// </summary>
    [Fact]
    public void Encrypt_ShouldReturnNonEmptyString()
    {
        var svc = CreateService();
        var result = svc.Encrypt("hello world");
        Assert.False(string.IsNullOrEmpty(result));
    }

    /// <summary>
    /// zh-CN: 验证加密后的数据能够被同一服务完整解密，守护基础往返语义。
    /// en: Verifies that encrypted data can be fully decrypted by the same service, guarding the basic round-trip contract.
    /// </summary>
    [Fact]
    public void Decrypt_ShouldReturnOriginalText()
    {
        var svc = CreateService();
        var encrypted = svc.Encrypt("secret-api-key");
        var decrypted = svc.Decrypt(encrypted);
        Assert.Equal("secret-api-key", decrypted);
    }

    /// <summary>
    /// zh-CN: 验证同一明文多次加密会产生不同密文，证明每次调用都会使用新的随机初始化向量。
    /// en: Verifies that encrypting the same plaintext multiple times yields different ciphertext, proving a fresh random IV is used for each call.
    /// </summary>
    [Fact]
    public void Encrypt_ShouldProduceDifferentCiphertextEachTime()
    {
        var svc = CreateService();
        var enc1 = svc.Encrypt("same text");
        var enc2 = svc.Encrypt("same text");

        // zh-CN: 每次加密都应生成不同的 IV，因此密文不应重复。
        // en: Each encryption should generate a different IV, so the ciphertext should differ.
        Assert.NotEqual(enc1, enc2);
    }

    /// <summary>
    /// zh-CN: 验证空字符串输入被原样返回，保持上层对“空值但非 null”语义的预期。
    /// en: Verifies that an empty string is returned unchanged, preserving caller expectations for "empty but not null" input.
    /// </summary>
    [Fact]
    public void Encrypt_EmptyString_ShouldReturnEmpty()
    {
        var svc = CreateService();
        var result = svc.Encrypt("");
        Assert.Equal("", result);
    }

    /// <summary>
    /// zh-CN: 验证解密空字符串时不会抛异常，保证与加密端的空值约定一致。
    /// en: Verifies that decrypting an empty string does not throw, keeping the empty-value contract aligned with encryption.
    /// </summary>
    [Fact]
    public void Decrypt_EmptyString_ShouldReturnEmpty()
    {
        var svc = CreateService();
        var result = svc.Decrypt("");
        Assert.Equal("", result);
    }

    /// <summary>
    /// zh-CN: 验证 null 输入会被透传为 null，方便调用方无需额外包装可空值。
    /// en: Verifies that null input is passed through as null so callers do not need extra nullable wrappers.
    /// </summary>
    [Fact]
    public void Encrypt_NullInput_ShouldReturnNull()
    {
        var svc = CreateService();
        var result = svc.Encrypt(null!);
        Assert.Null(result);
    }

    /// <summary>
    /// zh-CN: 验证长文本同样能够稳定往返，覆盖较大负载的分块与缓冲路径。
    /// en: Verifies that long text also round-trips correctly, covering buffering and payload-size paths.
    /// </summary>
    [Fact]
    public void RoundTrip_LongText_ShouldPreserveContent()
    {
        var svc = CreateService();
        var longText = new string('A', 10000);
        var encrypted = svc.Encrypt(longText);
        var decrypted = svc.Decrypt(encrypted);
        Assert.Equal(longText, decrypted);
    }

    /// <summary>
    /// zh-CN: 验证不同密钥之间不能互相解密，避免错误配置时悄悄返回伪造内容。
    /// en: Verifies that different keys cannot decrypt each other, preventing misconfiguration from silently returning bogus content.
    /// </summary>
    [Fact]
    public void DifferentKeys_ShouldNotDecryptEachOther()
    {
        var svc1 = new EncryptionService("key-one");
        var svc2 = new EncryptionService("key-two");

        var encrypted = svc1.Encrypt("secret");
        Assert.ThrowsAny<Exception>(() => svc2.Decrypt(encrypted));
    }
}
