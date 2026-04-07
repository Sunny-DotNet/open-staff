using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;
using OpenStaff.Infrastructure.Security;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Application.Providers;

public class ProviderAccountService
{
    private readonly AppDbContext _db;
    private readonly EncryptionService _encryption;
    private readonly IProtocolFactory _protocolFactory;
    private readonly bool _isDevelopment;

    public ProviderAccountService(
        AppDbContext db,
        EncryptionService encryption,
        IProtocolFactory protocolFactory,
        IHostEnvironment hostEnvironment)
    {
        _db = db;
        _encryption = encryption;
        _protocolFactory = protocolFactory;
        _isDevelopment = hostEnvironment.IsDevelopment();
    }

    private Func<string, string>? EncryptFunc => _isDevelopment ? null : _encryption.Encrypt;
    private Func<string, string>? DecryptFunc => _isDevelopment ? null : _encryption.Decrypt;

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
    /// 反序列化 EnvConfig（[Encrypted] 字段自动解密）
    /// </summary>
    public T? GetEnvConfig<T>(ProviderAccount account) where T : ProtocolEnvBase
    {
        if (string.IsNullOrEmpty(account.EnvConfig)) return null;
        return ProtocolEnvSerializer.Deserialize<T>(account.EnvConfig, DecryptFunc);
    }

    /// <summary>
    /// 反序列化 EnvConfig 为 Dictionary（[Encrypted] 字段自动解密）
    /// </summary>
    public Dictionary<string, object?>? GetEnvConfigDict(ProviderAccount account)
    {
        if (string.IsNullOrEmpty(account.EnvConfig)) return null;
        var envType = _protocolFactory.GetProtocolEnvType(account.ProtocolType);
        if (envType == null)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(account.EnvConfig,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        return ProtocolEnvSerializer.DeserializeToDict(account.EnvConfig, envType, DecryptFunc);
    }

    /// <summary>
    /// 解密 EnvConfig 为明文 JSON 字符串（[Encrypted] 字段自动解密）
    /// </summary>
    public string? DecryptEnvConfig(ProviderAccount account)
    {
        if (string.IsNullOrEmpty(account.EnvConfig)) return null;
        var dict = GetEnvConfigDict(account);
        return dict == null ? null : JsonSerializer.Serialize(dict);
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
            account.EnvConfig = SerializeEnvConfig(request.ProtocolType, request.EnvConfig);
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
            account.EnvConfig = SerializeEnvConfig(account.ProtocolType, request.EnvConfig);
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

        account.EnvConfig = SerializeEnvConfig(account.ProtocolType, dict);
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

    /// <summary>
    /// 序列化 EnvConfig（通过 ProtocolEnvType 确定哪些字段需要加密）
    /// </summary>
    private string SerializeEnvConfig(string protocolType, IDictionary<string, object?> envConfig)
    {
        var envType = _protocolFactory.GetProtocolEnvType(protocolType);
        if (envType == null)
        {
            return JsonSerializer.Serialize(envConfig);
        }

        // 先序列化为 JSON → 反序列化为强类型 → 再用 Serializer 加密序列化
        var json = JsonSerializer.Serialize(envConfig);
        var method = typeof(ProtocolEnvSerializer)
            .GetMethod(nameof(ProtocolEnvSerializer.Serialize))!
            .MakeGenericMethod(envType);
        var env = JsonSerializer.Deserialize(json, envType,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (env == null) return json;

        return (string)method.Invoke(null, [env, EncryptFunc])!;
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
