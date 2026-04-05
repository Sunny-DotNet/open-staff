using Microsoft.EntityFrameworkCore;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;
using OpenStaff.Infrastructure.Security;

namespace OpenStaff.Application.Providers;

public class DbProviderService
{
    private readonly AppDbContext _db;
    private readonly EncryptionService _encryption;

    public DbProviderService(AppDbContext db, EncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
    }

    public async Task<List<ModelProvider>> GetAllAsync()
    {
        return await _db.ModelProviders.OrderBy(p => p.CreatedAt).ToListAsync();
    }

    public async Task<ModelProvider?> GetByIdAsync(Guid id)
    {
        return await _db.ModelProviders.FindAsync(id);
    }

    public async Task<ModelProvider?> GetByTypeAsync(string providerType)
    {
        return await _db.ModelProviders.FirstOrDefaultAsync(p => p.ProviderType == providerType);
    }

    public async Task<ModelProvider?> GetFirstEnabledAsync()
    {
        return await _db.ModelProviders.FirstOrDefaultAsync(p => p.IsEnabled);
    }

    public string? ResolveApiKey(ModelProvider provider)
    {
        return provider.ApiKeyMode switch
        {
            ApiKeyModes.Input or ApiKeyModes.Device =>
                !string.IsNullOrEmpty(provider.ApiKeyEncrypted) ? _encryption.Decrypt(provider.ApiKeyEncrypted) : null,
            ApiKeyModes.EnvVar =>
                !string.IsNullOrEmpty(provider.ApiKeyEnvVar) ? Environment.GetEnvironmentVariable(provider.ApiKeyEnvVar) : null,
            _ => null
        };
    }

    public async Task<ModelProvider> CreateAsync(CreateProviderRequest request)
    {
        var provider = new ModelProvider
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ProviderType = request.ProviderType,
            BaseUrl = request.BaseUrl,
            ApiKeyMode = request.ApiKeyMode ?? ApiKeyModes.EnvVar,
            ApiKeyEnvVar = request.ApiKeyEnvVar,
            DefaultModel = request.DefaultModel,
            ExtraConfig = request.ExtraConfig,
            IsEnabled = request.IsEnabled,
            IsBuiltin = false
        };

        if (!string.IsNullOrEmpty(request.ApiKey))
        {
            provider.ApiKeyEncrypted = _encryption.Encrypt(request.ApiKey);
        }

        _db.ModelProviders.Add(provider);
        await _db.SaveChangesAsync();
        return provider;
    }

    public async Task<ModelProvider?> UpdateAsync(Guid id, UpdateProviderRequest request)
    {
        var provider = await _db.ModelProviders.FindAsync(id);
        if (provider == null) return null;

        if (request.Name != null) provider.Name = request.Name;
        if (request.BaseUrl != null) provider.BaseUrl = request.BaseUrl;
        if (request.ApiKeyMode != null) provider.ApiKeyMode = request.ApiKeyMode;
        if (request.ApiKeyEnvVar != null) provider.ApiKeyEnvVar = request.ApiKeyEnvVar;
        if (request.DefaultModel != null) provider.DefaultModel = request.DefaultModel;
        if (request.ExtraConfig != null) provider.ExtraConfig = request.ExtraConfig;
        if (request.IsEnabled.HasValue) provider.IsEnabled = request.IsEnabled.Value;
        provider.UpdatedAt = DateTime.UtcNow;

        if (request.ApiKey != null)
        {
            provider.ApiKeyEncrypted = _encryption.Encrypt(request.ApiKey);
        }

        await _db.SaveChangesAsync();
        return provider;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var provider = await _db.ModelProviders.FindAsync(id);
        if (provider == null) return false;

        _db.ModelProviders.Remove(provider);
        await _db.SaveChangesAsync();
        return true;
    }
}

// ===== Request DTOs =====

public class CreateProviderRequest
{
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public string? ApiKeyMode { get; set; }
    public string? ApiKeyEnvVar { get; set; }
    public string? ApiKey { get; set; }
    public string? DefaultModel { get; set; }
    public string? ExtraConfig { get; set; }
    public bool IsEnabled { get; set; } = false;
}

public class UpdateProviderRequest
{
    public string? Name { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKeyMode { get; set; }
    public string? ApiKeyEnvVar { get; set; }
    public string? ApiKey { get; set; }
    public string? DefaultModel { get; set; }
    public string? ExtraConfig { get; set; }
    public bool? IsEnabled { get; set; }
}
