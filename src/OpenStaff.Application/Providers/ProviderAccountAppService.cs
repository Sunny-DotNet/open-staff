using OpenStaff.Application.Contracts.Providers;
using OpenStaff.Application.Contracts.Providers.Dtos;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Application.Providers;

public class ProviderAccountAppService : IProviderAccountAppService
{
    private readonly ProviderAccountService _accountService;
    private readonly IProtocolFactory _protocolFactory;

    public ProviderAccountAppService(ProviderAccountService accountService, IProtocolFactory protocolFactory)
    {
        _accountService = accountService;
        _protocolFactory = protocolFactory;
    }

    public async Task<List<ProviderAccountDto>> GetAllAsync(CancellationToken ct)
    {
        var accounts = await _accountService.GetAllAsync();
        return accounts.Select(a => new ProviderAccountDto
        {
            Id = a.Id,
            Name = a.Name,
            ProtocolType = a.ProtocolType,
            IsEnabled = a.IsEnabled,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        }).ToList();
    }

    public async Task<ProviderAccountDetailDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var account = await _accountService.GetByIdAsync(id);
        if (account == null) return null;

        var envConfig = _accountService.GetEnvConfigDict(account);

        // 移除 secret 字段 — 前端不显示，保存时为空则忽略
        if (envConfig != null)
        {
            var metadata = _protocolFactory.GetProtocolMetadata()
                .FirstOrDefault(m => m.ProviderKey == account.ProtocolType);
            if (metadata != null)
            {
                foreach (var field in metadata.EnvSchema)
                {
                    if (field.FieldType == "secret")
                        envConfig.Remove(field.Name);
                }
            }
        }

        return new ProviderAccountDetailDto
        {
            Id = account.Id,
            Name = account.Name,
            ProtocolType = account.ProtocolType,
            IsEnabled = account.IsEnabled,
            EnvConfig = envConfig,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };
    }

    public async Task<ProviderAccountDto> CreateAsync(CreateProviderAccountInput input, CancellationToken ct)
    {
        var request = new CreateProviderAccountRequest
        {
            Name = input.Name,
            ProtocolType = input.ProtocolType,
            EnvConfig = input.EnvConfig,
            IsEnabled = input.IsEnabled
        };
        var account = await _accountService.CreateAsync(request);
        return new ProviderAccountDto
        {
            Id = account.Id,
            Name = account.Name,
            ProtocolType = account.ProtocolType,
            IsEnabled = account.IsEnabled,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };
    }

    public async Task<ProviderAccountDto?> UpdateAsync(Guid id, UpdateProviderAccountInput input, CancellationToken ct)
    {
        // secret 字段前端不提交（为空或不存在），需要回填原值
        if (input.EnvConfig != null)
        {
            var existing = await _accountService.GetByIdAsync(id);
            if (existing != null)
            {
                var oldEnv = _accountService.GetEnvConfigDict(existing);
                var metadata = _protocolFactory.GetProtocolMetadata()
                    .FirstOrDefault(m => m.ProviderKey == existing.ProtocolType);
                if (oldEnv != null && metadata != null)
                {
                    foreach (var field in metadata.EnvSchema)
                    {
                        if (field.FieldType != "secret") continue;
                        var hasNew = input.EnvConfig.TryGetValue(field.Name, out var newVal);
                        var newStr = newVal?.ToString();
                        // 前端未提交或为空 → 保留原值
                        if (!hasNew || string.IsNullOrEmpty(newStr))
                        {
                            if (oldEnv.TryGetValue(field.Name, out var oldVal))
                                input.EnvConfig[field.Name] = oldVal;
                        }
                    }
                }
            }
        }

        var request = new UpdateProviderAccountRequest
        {
            Name = input.Name,
            EnvConfig = input.EnvConfig,
            IsEnabled = input.IsEnabled
        };
        var account = await _accountService.UpdateAsync(id, request);
        if (account == null) return null;

        return new ProviderAccountDto
        {
            Id = account.Id,
            Name = account.Name,
            ProtocolType = account.ProtocolType,
            IsEnabled = account.IsEnabled,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        return await _accountService.DeleteAsync(id);
    }

    public async Task<List<ProviderModelDto>> ListModelsAsync(Guid id, CancellationToken ct)
    {
        var account = await _accountService.GetByIdAsync(id);
        if (account == null) throw new KeyNotFoundException($"Provider account {id} not found");

        var envJson = _accountService.DecryptEnvConfig(account) ?? "{}";
        var protocol = _protocolFactory.CreateProtocolWithEnv(account.ProtocolType, envJson);
        var models = await protocol.ModelsAsync(ct);

        return models.Select(m => new ProviderModelDto
        {
            Id = m.ModelSlug,
            Vendor = m.VenderSlug,
            Protocols = m.ModelProtocols.ToString()
        }).ToList();
    }
}
