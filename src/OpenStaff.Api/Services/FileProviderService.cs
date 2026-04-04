using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Security;

namespace OpenStaff.Api.Services;

/// <summary>
/// 文件系统供应商服务 — 将配置存储在 ~/.staff/providers/ 目录
/// File-based provider service — stores configs in ~/.staff/providers/
/// </summary>
public class FileProviderService
{
    private readonly string _providersDir;
    private readonly EncryptionService _encryption;
    private readonly ConcurrentDictionary<Guid, ModelProvider> _cache = new();
    private bool _initialized;

    public FileProviderService(EncryptionService encryption)
    {
        _encryption = encryption;
        var staffDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".staff");
        _providersDir = Path.Combine(staffDir, "providers");
        Directory.CreateDirectory(_providersDir);
    }

    /// <summary>
    /// 获取所有供应商 / Get all providers
    /// </summary>
    public List<ModelProvider> GetAll()
    {
        EnsureLoaded();
        return _cache.Values.OrderBy(p => p.CreatedAt).ToList();
    }

    /// <summary>
    /// 按 ID 查找 / Find by ID
    /// </summary>
    public ModelProvider? GetById(Guid id)
    {
        EnsureLoaded();
        return _cache.TryGetValue(id, out var p) ? p : null;
    }

    /// <summary>
    /// 按供应商类型查找 / Find by provider type
    /// </summary>
    public ModelProvider? GetByType(string providerType)
    {
        EnsureLoaded();
        return _cache.Values.FirstOrDefault(p => p.ProviderType == providerType);
    }

    /// <summary>
    /// 获取第一个已启用的供应商 / Get first enabled provider
    /// </summary>
    public ModelProvider? GetFirstEnabled()
    {
        EnsureLoaded();
        return _cache.Values.FirstOrDefault(p => p.IsEnabled);
    }

    /// <summary>
    /// 创建供应商 / Create provider
    /// </summary>
    public ModelProvider Create(CreateProviderRequest request)
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

        SaveProvider(provider);
        _cache[provider.Id] = provider;
        return provider;
    }

    /// <summary>
    /// 更新供应商 / Update provider
    /// </summary>
    public ModelProvider? Update(Guid id, UpdateProviderRequest request)
    {
        if (!_cache.TryGetValue(id, out var provider)) return null;

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

        SaveProvider(provider);
        return provider;
    }

    /// <summary>
    /// 删除供应商 / Delete provider
    /// </summary>
    public bool Delete(Guid id)
    {
        if (!_cache.TryRemove(id, out var provider)) return false;

        var configFile = GetConfigPath(provider.ProviderType);
        var keyFile = GetKeyPath(provider.ProviderType);
        if (File.Exists(configFile)) File.Delete(configFile);
        if (File.Exists(keyFile)) File.Delete(keyFile);
        return true;
    }

    /// <summary>
    /// 种子默认供应商（只创建不存在的）/ Seed defaults (only if not exist)
    /// </summary>
    public void SeedDefaults()
    {
        EnsureLoaded();

        var defaults = new[]
        {
            new ModelProvider
            {
                Name = "OpenAI",
                ProviderType = ProviderTypes.OpenAI,
                BaseUrl = "https://api.openai.com/v1",
                ApiKeyMode = ApiKeyModes.EnvVar,
                ApiKeyEnvVar = "OPENAI_API_KEY",
                DefaultModel = "gpt-4o",
                IsEnabled = false,
                IsBuiltin = true
            },
            new ModelProvider
            {
                Name = "Google",
                ProviderType = ProviderTypes.Google,
                BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
                ApiKeyMode = ApiKeyModes.EnvVar,
                ApiKeyEnvVar = "GOOGLE_API_KEY",
                DefaultModel = "gemini-2.0-flash",
                IsEnabled = false,
                IsBuiltin = true
            },
            new ModelProvider
            {
                Name = "Anthropic",
                ProviderType = ProviderTypes.Anthropic,
                BaseUrl = "https://api.anthropic.com/v1",
                ApiKeyMode = ApiKeyModes.EnvVar,
                ApiKeyEnvVar = "ANTHROPIC_API_KEY",
                DefaultModel = "claude-sonnet-4-20250514",
                IsEnabled = false,
                IsBuiltin = true
            },
            new ModelProvider
            {
                Name = "GitHub Copilot",
                ProviderType = ProviderTypes.GitHubCopilot,
                BaseUrl = "https://api.githubcopilot.com",
                ApiKeyMode = ApiKeyModes.Device,
                DefaultModel = "gpt-4o",
                IsEnabled = false,
                IsBuiltin = true
            }
        };

        var existingTypes = _cache.Values.Select(p => p.ProviderType).ToHashSet();
        foreach (var provider in defaults)
        {
            if (!existingTypes.Contains(provider.ProviderType))
            {
                SaveProvider(provider);
                _cache[provider.Id] = provider;
            }
        }
    }

    // ===== 文件 I/O =====

    private void EnsureLoaded()
    {
        if (_initialized) return;
        _initialized = true;

        foreach (var configFile in Directory.GetFiles(_providersDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(configFile);
                var dto = JsonSerializer.Deserialize<ProviderFileDto>(json, JsonOpts);
                if (dto == null) continue;

                var provider = dto.ToModel();

                // 读取加密 key 文件
                var keyFile = Path.ChangeExtension(configFile, ".key");
                if (File.Exists(keyFile))
                {
                    provider.ApiKeyEncrypted = File.ReadAllText(keyFile).Trim();
                }

                _cache[provider.Id] = provider;
            }
            catch
            {
                // 跳过损坏的配置文件
            }
        }
    }

    private void SaveProvider(ModelProvider provider)
    {
        var dto = ProviderFileDto.FromModel(provider);
        var configPath = GetConfigPath(provider.ProviderType);
        var json = JsonSerializer.Serialize(dto, JsonOpts);
        File.WriteAllText(configPath, json);

        // API key 单独存储
        var keyPath = GetKeyPath(provider.ProviderType);
        if (!string.IsNullOrEmpty(provider.ApiKeyEncrypted))
        {
            File.WriteAllText(keyPath, provider.ApiKeyEncrypted);
        }
        else if (File.Exists(keyPath))
        {
            File.Delete(keyPath);
        }
    }

    private string GetConfigPath(string providerType) => Path.Combine(_providersDir, $"{providerType}.json");
    private string GetKeyPath(string providerType) => Path.Combine(_providersDir, $"{providerType}.key");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>
/// 供应商配置文件 DTO（不含加密 key）
/// </summary>
internal class ProviderFileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public string ApiKeyMode { get; set; } = ApiKeyModes.EnvVar;
    public string? ApiKeyEnvVar { get; set; }
    public string? DefaultModel { get; set; }
    public string? ExtraConfig { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsBuiltin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ModelProvider ToModel() => new()
    {
        Id = Id,
        Name = Name,
        ProviderType = ProviderType,
        BaseUrl = BaseUrl,
        ApiKeyMode = ApiKeyMode,
        ApiKeyEnvVar = ApiKeyEnvVar,
        DefaultModel = DefaultModel,
        ExtraConfig = ExtraConfig,
        IsEnabled = IsEnabled,
        IsBuiltin = IsBuiltin,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };

    public static ProviderFileDto FromModel(ModelProvider p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        ProviderType = p.ProviderType,
        BaseUrl = p.BaseUrl,
        ApiKeyMode = p.ApiKeyMode,
        ApiKeyEnvVar = p.ApiKeyEnvVar,
        DefaultModel = p.DefaultModel,
        ExtraConfig = p.ExtraConfig,
        IsEnabled = p.IsEnabled,
        IsBuiltin = p.IsBuiltin,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
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
