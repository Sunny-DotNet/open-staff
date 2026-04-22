using System.Security.Cryptography;
using System.Text;

namespace OpenStaff.Infrastructure.Security;

/// <summary>
/// 使用 AES-256 对敏感配置进行可逆加密。
/// Uses AES-256 to encrypt sensitive configuration values.
/// </summary>
public class EncryptionService
{
    private readonly byte[] _key;

    /// <summary>
    /// 使用应用密钥初始化加密服务。
    /// Initializes the encryption service with an application secret.
    /// </summary>
    /// <param name="encryptionKey">来自安全配置的原始密钥。/ The raw secret sourced from secure configuration.</param>
    public EncryptionService(string encryptionKey)
    {
        // zh-CN: 通过 SHA-256 将任意长度口令折叠为稳定的 32 字节 AES 密钥。
        // en: Fold an arbitrary-length secret into a stable 32-byte AES key via SHA-256.
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(encryptionKey));
    }

    /// <summary>
    /// 加密文本。
    /// Encrypts plain text.
    /// </summary>
    /// <param name="plainText">待加密的明文。/ The plain text to encrypt.</param>
    /// <returns>Base64 编码的密文。/ The Base64-encoded ciphertext.</returns>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // zh-CN: 将随机 IV 前置到密文前面，解密时无需单独存储额外元数据。
        // en: Prefix the random IV to the ciphertext so decryption does not need separate metadata storage.
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// 解密文本。
    /// Decrypts ciphertext.
    /// </summary>
    /// <param name="cipherText">Base64 编码的密文。/ The Base64-encoded ciphertext.</param>
    /// <returns>解密后的明文。/ The decrypted plain text.</returns>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        var fullBytes = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _key;

        // zh-CN: 前 16 字节存放 IV，剩余部分才是真正的密文载荷。
        // en: The first 16 bytes store the IV, and the remaining bytes contain the actual ciphertext payload.
        var iv = new byte[aes.BlockSize / 8];
        var cipher = new byte[fullBytes.Length - iv.Length];
        Buffer.BlockCopy(fullBytes, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(fullBytes, iv.Length, cipher, 0, cipher.Length);

        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
