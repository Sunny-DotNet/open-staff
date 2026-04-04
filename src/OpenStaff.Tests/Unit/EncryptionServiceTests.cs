using OpenStaff.Infrastructure.Security;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class EncryptionServiceTests
{
    private static EncryptionService CreateService()
    {
        return new EncryptionService("test-encryption-key-for-unit-tests");
    }

    [Fact]
    public void Encrypt_ShouldReturnNonEmptyString()
    {
        var svc = CreateService();
        var result = svc.Encrypt("hello world");
        Assert.False(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void Decrypt_ShouldReturnOriginalText()
    {
        var svc = CreateService();
        var encrypted = svc.Encrypt("secret-api-key");
        var decrypted = svc.Decrypt(encrypted);
        Assert.Equal("secret-api-key", decrypted);
    }

    [Fact]
    public void Encrypt_ShouldProduceDifferentCiphertextEachTime()
    {
        var svc = CreateService();
        var enc1 = svc.Encrypt("same text");
        var enc2 = svc.Encrypt("same text");
        Assert.NotEqual(enc1, enc2); // IV should differ each time
    }

    [Fact]
    public void Encrypt_EmptyString_ShouldReturnEmpty()
    {
        var svc = CreateService();
        var result = svc.Encrypt("");
        Assert.Equal("", result);
    }

    [Fact]
    public void Decrypt_EmptyString_ShouldReturnEmpty()
    {
        var svc = CreateService();
        var result = svc.Decrypt("");
        Assert.Equal("", result);
    }

    [Fact]
    public void Encrypt_NullInput_ShouldReturnNull()
    {
        var svc = CreateService();
        var result = svc.Encrypt(null!);
        Assert.Null(result);
    }

    [Fact]
    public void RoundTrip_LongText_ShouldPreserveContent()
    {
        var svc = CreateService();
        var longText = new string('A', 10000);
        var encrypted = svc.Encrypt(longText);
        var decrypted = svc.Decrypt(encrypted);
        Assert.Equal(longText, decrypted);
    }

    [Fact]
    public void DifferentKeys_ShouldNotDecryptEachOther()
    {
        var svc1 = new EncryptionService("key-one");
        var svc2 = new EncryptionService("key-two");

        var encrypted = svc1.Encrypt("secret");
        Assert.ThrowsAny<Exception>(() => svc2.Decrypt(encrypted));
    }
}
