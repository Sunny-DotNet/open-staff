using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenStaff.Entities;
using OpenStaff.Infrastructure.Security;
using OpenStaff.Options;
using OpenStaff.Provider.Protocols;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Providers.Services;
/// <summary>
/// 提供商账户存储服务，负责账户持久化和配置加解密。
/// Provider-account storage service that persists accounts and encrypts or decrypts configuration values.
/// </summary>
public class ProviderAccountService
{
    private readonly IProviderAccountRepository _providerAccounts;
    private readonly IRepositoryContext _repositoryContext;
    private readonly EncryptionService _encryption;
    private readonly IProtocolFactory _protocolFactory;
    private readonly OpenStaffOptions _openStaffOptions;
    private readonly bool _isDevelopment;

    /// <summary>
    /// Initializes the scoped provider-account storage service with persistence, protocol metadata, and environment-sensitive encryption behavior for local development versus deployed environments.
    /// 使用持久化能力、协议元数据以及面向本地开发与部署环境的加密行为初始化 Scoped 提供商账户存储服务。
    /// </summary>
    /// <param name="providerAccounts">Repository that stores provider accounts. / 存储提供商账户的仓储。</param>
    /// <param name="repositoryContext">Unit-of-work context used to commit provider-account changes. / 用于提交提供商账户变更的工作单元上下文。</param>
    /// <param name="encryption">Encryption service used outside development to protect sensitive env/config fields. / 在非开发环境下用于保护敏感环境/配置字段的加密服务。</param>
    /// <param name="protocolFactory">Protocol factory used to discover typed env/config schemas. / 用于发现强类型环境/配置架构的协议工厂。</param>
    /// <param name="hostEnvironment">Host environment used to decide whether encryption should be bypassed for local development. / 用于决定本地开发是否跳过加密的宿主环境。</param>
    public ProviderAccountService(
        IProviderAccountRepository providerAccounts,
        IRepositoryContext repositoryContext,
        EncryptionService encryption,
        IProtocolFactory protocolFactory,
        IHostEnvironment hostEnvironment,
        IOptions<OpenStaffOptions> openStaffOptions)
    {
        _providerAccounts = providerAccounts;
        _repositoryContext = repositoryContext;
        _encryption = encryption;
        _protocolFactory = protocolFactory;
        _openStaffOptions = openStaffOptions.Value;
        _isDevelopment = hostEnvironment.IsDevelopment();
    }

    private Func<string, string>? EncryptFunc => _isDevelopment ? null : _encryption.Encrypt;
    private Func<string, string>? DecryptFunc => _isDevelopment ? null : _encryption.Decrypt;
    private string ProviderConfigDirectoryPath => Path.Combine(_openStaffOptions.WorkingDirectory, "providers");

    /// <summary>
    /// 获取所有提供商账户。
    /// Gets all provider accounts.
    /// </summary>
    public async Task<List<ProviderAccount>> GetAllAsync()
    {
        return await _providerAccounts.OrderBy(p => p.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// 根据标识获取提供商账户。
    /// Gets a provider account by identifier.
    /// </summary>
    public async Task<ProviderAccount?> GetByIdAsync(Guid id)
    {
        return await _providerAccounts.FindAsync(id);
    }

    /// <summary>
    /// 根据协议类型获取提供商账户。
    /// Gets a provider account by protocol type.
    /// </summary>
    public async Task<ProviderAccount?> GetByProtocolTypeAsync(string protocolType)
    {
        return await _providerAccounts.FirstOrDefaultAsync(p => p.ProtocolType == protocolType);
    }

    /// <summary>
    /// 获取第一个启用的提供商账户。
    /// Gets the first enabled provider account.
    /// </summary>
    public async Task<ProviderAccount?> GetFirstEnabledAsync()
    {
        return await _providerAccounts.FirstOrDefaultAsync(p => p.IsEnabled);
    }

    /// <summary>
    /// 反序列化 EnvConfig，并自动解密带 <c>[Encrypted]</c> 标记的字段。
    /// Deserializes EnvConfig and automatically decrypts fields marked with <c>[Encrypted]</c>.
    /// </summary>
    public async Task<T?> GetEnvConfigAsync<T>(ProviderAccount account, CancellationToken ct = default) where T : ProtocolEnvBase
    {
        var envConfig = await ReadRawEnvConfigAsync(account.Id, ct);
        if (string.IsNullOrEmpty(envConfig)) return null;
        return ProtocolEnvSerializer.Deserialize<T>(envConfig, DecryptFunc);
    }

    /// <summary>
    /// 将 EnvConfig 反序列化为字典，并自动解密受保护字段。
    /// Deserializes EnvConfig into a dictionary and automatically decrypts protected fields.
    /// </summary>
    public async Task<Dictionary<string, object?>?> GetEnvConfigDictAsync(ProviderAccount account, CancellationToken ct = default)
    {
        var envConfig = await ReadRawEnvConfigAsync(account.Id, ct);
        if (string.IsNullOrEmpty(envConfig)) return null;

        var envType = _protocolFactory.GetProtocolEnvType(account.ProtocolType);
        if (envType == null)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(envConfig,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        return ProtocolEnvSerializer.DeserializeToDict(envConfig, envType, DecryptFunc);
    }

    /// <summary>
    /// 将 EnvConfig 解密为明文 JSON 字符串。
    /// Decrypts EnvConfig into a plain JSON string.
    /// </summary>
    public async Task<string?> DecryptEnvConfigAsync(ProviderAccount account, CancellationToken ct = default)
    {
        var dict = await GetEnvConfigDictAsync(account, ct);
        return dict == null ? null : JsonSerializer.Serialize(dict);
    }

    /// <summary>
    /// 创建提供商账户。
    /// Creates a provider account.
    /// </summary>
    public async Task<ProviderAccount> CreateAsync(CreateProviderAccountRequest request, CancellationToken ct = default)
    {
        var account = new ProviderAccount
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ProtocolType = request.ProtocolType,
            IsEnabled = request.IsEnabled,
        };

        var envConfig = request.EnvConfig == null
            ? null
            : SerializeEnvConfig(request.ProtocolType, request.EnvConfig);

        if (envConfig != null)
            await WriteEnvConfigAsync(account.Id, envConfig, ct);

        _providerAccounts.Add(account);
        try
        {
            await _repositoryContext.SaveChangesAsync(ct);
        }
        catch
        {
            if (envConfig != null)
                DeleteEnvConfigFile(account.Id);

            throw;
        }

        return account;
    }

    /// <summary>
    /// 更新提供商账户。
    /// Updates a provider account.
    /// </summary>
    public async Task<ProviderAccount?> UpdateAsync(Guid id, UpdateProviderAccountRequest request, CancellationToken ct = default)
    {
        var account = await _providerAccounts.FindAsync(id);
        if (account == null) return null;

        string? previousEnvConfig = null;
        if (request.EnvConfig != null)
        {
            previousEnvConfig = await ReadRawEnvConfigAsync(account.Id, ct);
            await WriteEnvConfigAsync(account.Id, SerializeEnvConfig(account.ProtocolType, request.EnvConfig), ct);
        }

        if (request.Name != null) account.Name = request.Name;
        if (request.IsEnabled.HasValue) account.IsEnabled = request.IsEnabled.Value;

        account.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _repositoryContext.SaveChangesAsync(ct);
        }
        catch
        {
            if (request.EnvConfig != null)
                await RestoreEnvConfigAsync(account.Id, previousEnvConfig, ct);

            throw;
        }

        return account;
    }

    /// <summary>
    /// 更新 EnvConfig 中的单个字段。
    /// Updates a single field inside EnvConfig.
    /// </summary>
    public async Task UpdateEnvConfigFieldAsync(Guid id, string fieldName, string value, CancellationToken ct = default)
    {
        var account = await _providerAccounts.FindAsync(id);
        if (account == null) return;

        var previousEnvConfig = await ReadRawEnvConfigAsync(account.Id, ct);
        var dict = await GetEnvConfigDictAsync(account, ct) ?? new Dictionary<string, object?>();
        dict[fieldName] = value;

        await WriteEnvConfigAsync(account.Id, SerializeEnvConfig(account.ProtocolType, dict), ct);
        account.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _repositoryContext.SaveChangesAsync(ct);
        }
        catch
        {
            await RestoreEnvConfigAsync(account.Id, previousEnvConfig, ct);
            throw;
        }
    }

    /// <summary>
    /// 删除提供商账户。
    /// Deletes a provider account.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var account = await _providerAccounts.FindAsync(id);
        if (account == null) return false;

        var previousEnvConfig = await ReadRawEnvConfigAsync(account.Id, ct);
        DeleteEnvConfigFile(account.Id);
        _providerAccounts.Remove(account);
        try
        {
            await _repositoryContext.SaveChangesAsync(ct);
            return true;
        }
        catch
        {
            await RestoreEnvConfigAsync(account.Id, previousEnvConfig, ct);
            throw;
        }
    }

    /// <summary>
    /// 序列化 EnvConfig，并根据协议类型决定哪些字段需要加密。
    /// Serializes EnvConfig and determines which fields should be encrypted based on the protocol type.
    /// </summary>
    private string SerializeEnvConfig(string protocolType, IDictionary<string, object?> envConfig)
    {
        var envType = _protocolFactory.GetProtocolEnvType(protocolType);
        if (envType == null)
        {
            return JsonSerializer.Serialize(envConfig);
        }

        // zh-CN: 先投影到协议强类型，才能复用 [Encrypted] 元数据判断哪些字段需要保护。
        // en: Project into the protocol-specific strong type first so the serializer can honor [Encrypted] metadata for protected fields.
        var json = JsonSerializer.Serialize(envConfig);
        var method = typeof(ProtocolEnvSerializer)
            .GetMethod(nameof(ProtocolEnvSerializer.Serialize))!
            .MakeGenericMethod(envType);
        var env = JsonSerializer.Deserialize(json, envType,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (env == null) return json;

        return (string)method.Invoke(null, [env, EncryptFunc])!;
    }

    private string GetEnvConfigFilePath(Guid providerAccountId)
        => Path.Combine(ProviderConfigDirectoryPath, $"{providerAccountId}.json");

    internal async Task<string?> ReadRawEnvConfigAsync(Guid providerAccountId, CancellationToken ct = default)
    {
        var filePath = GetEnvConfigFilePath(providerAccountId);
        if (!File.Exists(filePath))
            return null;

        return await File.ReadAllTextAsync(filePath, ct);
    }

    private async Task WriteEnvConfigAsync(Guid providerAccountId, string envConfig, CancellationToken ct)
    {
        Directory.CreateDirectory(ProviderConfigDirectoryPath);

        var filePath = GetEnvConfigFilePath(providerAccountId);
        var tempFilePath = $"{filePath}.tmp";
        await File.WriteAllTextAsync(tempFilePath, envConfig, ct);
        File.Move(tempFilePath, filePath, overwrite: true);
    }

    private async Task RestoreEnvConfigAsync(Guid providerAccountId, string? previousEnvConfig, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(previousEnvConfig))
        {
            DeleteEnvConfigFile(providerAccountId);
            return;
        }

        await WriteEnvConfigAsync(providerAccountId, previousEnvConfig, ct);
    }

    internal Task RestoreRawEnvConfigAsync(Guid providerAccountId, string? previousEnvConfig, CancellationToken ct = default)
        => RestoreEnvConfigAsync(providerAccountId, previousEnvConfig, ct);

    private void DeleteEnvConfigFile(Guid providerAccountId)
    {
        var filePath = GetEnvConfigFilePath(providerAccountId);
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}

/// <summary>
/// 创建提供商账户的内部请求模型。
/// Internal request model used to create a provider account.
/// </summary>
public class CreateProviderAccountRequest
{
    /// <summary>账户名称。 / Account name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>协议类型。 / Protocol type.</summary>
    public string ProtocolType { get; set; } = string.Empty;

    /// <summary>环境配置。 / Environment configuration.</summary>
    public Dictionary<string, object?>? EnvConfig { get; set; }

    /// <summary>是否启用。 / Whether the account is enabled.</summary>
    public bool IsEnabled { get; set; } = false;
}

/// <summary>
/// 更新提供商账户的内部请求模型。
/// Internal request model used to update a provider account.
/// </summary>
public class UpdateProviderAccountRequest
{
    /// <summary>账户名称。 / Account name.</summary>
    public string? Name { get; set; }

    /// <summary>环境配置。 / Environment configuration.</summary>
    public Dictionary<string, object?>? EnvConfig { get; set; }

    /// <summary>是否启用。 / Whether the account is enabled.</summary>
    public bool? IsEnabled { get; set; }
}

