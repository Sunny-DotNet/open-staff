using Microsoft.EntityFrameworkCore;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;
using OpenStaff.Infrastructure.Security;

namespace OpenStaff.Api.Services;

/// <summary>
/// 设置应用服务 / Settings application service
/// </summary>
public class SettingsService
{
    private readonly AppDbContext _db;
    private readonly EncryptionService _encryption;

    public SettingsService(AppDbContext db, EncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
    }

    // 全局设置 / Global Settings
    public async Task<List<GlobalSetting>> GetAllSettingsAsync(CancellationToken ct)
    {
        return await _db.GlobalSettings.OrderBy(s => s.Category).ThenBy(s => s.Key).ToListAsync(ct);
    }

    public async Task UpdateSettingsAsync(Dictionary<string, string> settings, CancellationToken ct)
    {
        foreach (var (key, value) in settings)
        {
            var existing = await _db.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.GlobalSettings.Add(new GlobalSetting { Key = key, Value = value });
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    // 模型供应商 / Model Providers
    public async Task<List<ModelProvider>> GetAllProvidersAsync(CancellationToken ct)
    {
        return await _db.ModelProviders.Where(p => p.IsActive).ToListAsync(ct);
    }

    public async Task<ModelProvider> CreateProviderAsync(CreateProviderRequest request, CancellationToken ct)
    {
        var provider = new ModelProvider
        {
            Name = request.Name,
            ProviderType = request.ProviderType,
            BaseUrl = request.BaseUrl,
            ApiKeyEncrypted = _encryption.Encrypt(request.ApiKey),
            DefaultModel = request.DefaultModel,
            ExtraConfig = request.ExtraConfig
        };

        _db.ModelProviders.Add(provider);
        await _db.SaveChangesAsync(ct);
        return provider;
    }

    public async Task<ModelProvider?> UpdateProviderAsync(Guid id, UpdateProviderRequest request, CancellationToken ct)
    {
        var provider = await _db.ModelProviders.FindAsync(new object[] { id }, ct);
        if (provider == null) return null;

        if (request.Name != null) provider.Name = request.Name;
        if (request.BaseUrl != null) provider.BaseUrl = request.BaseUrl;
        if (request.ApiKey != null) provider.ApiKeyEncrypted = _encryption.Encrypt(request.ApiKey);
        if (request.DefaultModel != null) provider.DefaultModel = request.DefaultModel;
        if (request.ExtraConfig != null) provider.ExtraConfig = request.ExtraConfig;
        provider.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return provider;
    }

    public async Task<bool> DeleteProviderAsync(Guid id, CancellationToken ct)
    {
        var provider = await _db.ModelProviders.FindAsync(new object[] { id }, ct);
        if (provider == null) return false;

        provider.IsActive = false; // 软删除 / Soft delete
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public class CreateProviderRequest
{
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string? DefaultModel { get; set; }
    public string? ExtraConfig { get; set; }
}

public class UpdateProviderRequest
{
    public string? Name { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? DefaultModel { get; set; }
    public string? ExtraConfig { get; set; }
}
