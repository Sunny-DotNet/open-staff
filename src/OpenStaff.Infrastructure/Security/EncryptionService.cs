using System.Security.Cryptography;
using System.Text;

namespace OpenStaff.Infrastructure.Security;

/// <summary>
/// AES-256 加密服务 — 用于 API Key 等敏感数据加密存储
/// AES-256 encryption service — for encrypting sensitive data like API keys
/// </summary>
public class EncryptionService
{
    private readonly byte[] _key;

    /// <summary>
    /// 使用加密密钥初始化 / Initialize with encryption key
    /// 密钥应从环境变量或安全存储获取 / Key should come from env var or secure storage
    /// </summary>
    public EncryptionService(string encryptionKey)
    {
        // 使用 SHA-256 确保密钥长度为 32 字节 / Use SHA-256 to ensure 32-byte key
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(encryptionKey));
    }

    /// <summary>
    /// 加密文本 / Encrypt text
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // IV + 密文一起存储 / Store IV + ciphertext together
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// 解密文本 / Decrypt text
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        var fullBytes = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _key;

        // 提取 IV / Extract IV
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
