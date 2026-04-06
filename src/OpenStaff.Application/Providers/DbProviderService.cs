using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;
using OpenStaff.Infrastructure.Security;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Application.Providers;

public class ProviderAccountService
{
    private readonly AppDbContext _db;
    private readonly EncryptionService _encryption;

    public ProviderAccountService(AppDbContext db, EncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
    }

    public async Task<List<ProviderAccount>> GetAllAsync()
    {
        return await _db.ProviderAccounts.OrderBy(p => p.CreatedAt).ToListAsync();
    }

    public async Task<ProviderAccount?> GetByIdAsync(Guid id)
    {
        return await _db.ProviderAccounts.FindAsync(id);
    }

    public async Task<ProviderAccount?> GetByProtocolTypeAsync(string protocolType)
    {
        return await _db.ProviderAccounts.FirstOrDefaultAsync(p => p.ProtocolType == protocolType);
    }

    public async Task<ProviderAccount?> GetFirstEnabledAsync()
    {
        return await _db.ProviderAccounts.FirstOrDefaultAsync(p => p.IsEnabled);
    }

    /// <summary>
    /// 解密并反序列化 EnvConfig
    /// </summary>
    public T? GetEnvConfig<T>(ProviderAccount account) where T : ProtocolEnvBase
    {
        if (string.IsNullOrEmpty(account.EnvConfigEncrypted)) return null;
        var json = _encryption.Decrypt(account.EnvConfigEncrypted);
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    /// <summary>
    /// 解密 EnvConfig 为 JSON 字符串
    /// </summary>
    public string? DecryptEnvConfig(ProviderAccount account)
    {
        if (string.IsNullOrEmpty(account.EnvConfigEncrypted)) return null;
        return _encryption.Decrypt(account.EnvConfigEncrypted);
    }

    /// <summary>
    /// 解密 EnvConfig 为 Dictionary（用于返回给前端，去除敏感值）
    /// </summary>
    public Dictionary<string, object?>? GetEnvConfigDict(ProviderAccount account)
    {
        var json = DecryptEnvConfig(account);
        if (json == null) return null;
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<ProviderAccount> CreateAsync(CreateProviderAccountRequest request)
    {
        var account = new ProviderAccount
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ProtocolType = request.ProtocolType,
            IsEnabled = request.IsEnabled,
        };

        if (request.EnvConfig != null)
        {
            var json = JsonSerializer.Serialize(request.EnvConfig);
            account.EnvConfigEncrypted = _encryption.Encrypt(json);
        }

        _db.ProviderAccounts.Add(account);
        await _db.SaveChangesAsync();
        return account;
    }

    public async Task<ProviderAccount?> UpdateAsync(Guid id, UpdateProviderAccountRequest request)
    {
        var account = await _db.ProviderAccounts.FindAsync(id);
        if (account == null) return null;

        if (request.Name != null) account.Name = request.Name;
        if (request.IsEnabled.HasValue) account.IsEnabled = request.IsEnabled.Value;

        if (request.EnvConfig != null)
        {
            var json = JsonSerializer.Serialize(request.EnvConfig);
            account.EnvConfigEncrypted = _encryption.Encrypt(json);
        }

        account.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return account;
    }

    /// <summary>
    /// 更新 EnvConfig 中的单个字段（用于 device-auth 回写 token）
    /// </summary>
    public async Task UpdateEnvConfigFieldAsync(Guid id, string fieldName, string value)
    {
        var account = await _db.ProviderAccounts.FindAsync(id);
        if (account == null) return;

        var dict = GetEnvConfigDict(account) ?? new Dictionary<string, object?>();
        dict[fieldName] = value;

        var json = JsonSerializer.Serialize(dict);
        account.EnvConfigEncrypted = _encryption.Encrypt(json);
        account.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var account = await _db.ProviderAccounts.FindAsync(id);
        if (account == null) return false;

        _db.ProviderAccounts.Remove(account);
        await _db.SaveChangesAsync();
        return true;
    }
}

// ===== Request DTOs =====

public class CreateProviderAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public string ProtocolType { get; set; } = string.Empty;
    public Dictionary<string, object>? EnvConfig { get; set; }
    public bool IsEnabled { get; set; } = false;
}

public class UpdateProviderAccountRequest
{
    public string? Name { get; set; }
    public Dictionary<string, object>? EnvConfig { get; set; }
    public bool? IsEnabled { get; set; }
}
