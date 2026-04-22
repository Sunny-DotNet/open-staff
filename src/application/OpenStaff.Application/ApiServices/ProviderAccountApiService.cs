using OpenStaff.Application.Providers;
using OpenStaff.Application.Providers.Services;

namespace OpenStaff.ApiServices;
/// <summary>
/// 提供商账户应用服务实现。
/// Application service implementation for provider accounts.
/// </summary>
public class ProviderAccountApiService :CrudApiServiceBase<ProviderAccount, ProviderAccountDto, Guid, ProviderAccountQueryInput, CreateProviderAccountInput, UpdateProviderAccountInput>, IProviderAccountApiService
{
    private static readonly ProviderAccountMapper Mapper = new();
    private readonly ProviderAccountConfigurationService _providerAccountConfigurationService;
    private readonly ProviderAccountService _providerAccountService;
    protected IPlatformRegistry PlatformRegistry { get; }

    public ProviderAccountApiService(
        IServiceProvider serviceProvider,
        IRepository<ProviderAccount, Guid> repository,
        IRepositoryContext repositoryContext,
        ProviderAccountService providerAccountService,
        ProviderAccountConfigurationService providerAccountConfigurationService,
        IPlatformRegistry platformRegistry) : base(serviceProvider, repository, repositoryContext)
    {
        _providerAccountConfigurationService = providerAccountConfigurationService;
        _providerAccountService = providerAccountService;
        PlatformRegistry = platformRegistry;
    }

    /// <inheritdoc />
    public Task<List<ProviderInfo>> GetAllProvidersAsync(CancellationToken ct = default)
    {
        return Task.FromResult(PlatformRegistry.GetProtocols().Select(p => new ProviderInfo
        {
            Key = p.ProtocolKey,
            DisplayName = p.ProtocolName,
            Logo = p.Logo,
            Description = string.Empty,
        }).ToList());
    }

    protected override IQueryable<ProviderAccount> ApplyFiltering(IQueryable<ProviderAccount> queryable, ProviderAccountQueryInput input)
    {
        if (input.ProtocolTypes is { Count: > 0 })
            queryable = queryable.Where(account => input.ProtocolTypes.Contains(account.ProtocolType));

        if (input.IsEnabled.HasValue)
            queryable = queryable.Where(account => account.IsEnabled == input.IsEnabled.Value);

        return queryable;
    }

    protected override ProviderAccountDto MapToDto(ProviderAccount account) => Mapper.ToDto(account);

    protected override ProviderAccount MapToEntity(CreateProviderAccountInput input) => Mapper.ToEntity(input);

    protected override ProviderAccount MapToEntity(UpdateProviderAccountInput input, ProviderAccount entity)
    {
        Mapper.Apply(input, entity);
        return entity;
    }

    public override async Task<ProviderAccountDto> CreateAsync(CreateProviderAccountInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _providerAccountService.CreateAsync(new CreateProviderAccountRequest
        {
            Name = input.Name,
            ProtocolType = input.ProtocolType,
            IsEnabled = input.IsEnabled
        }, cancellationToken);
        return MapToDto(entity);
    }

    public override async Task<ProviderAccountDto> UpdateAsync(Guid id, UpdateProviderAccountInput input, CancellationToken cancellationToken = default)
    {
        var entity = await _providerAccountService.UpdateAsync(id, new UpdateProviderAccountRequest
        {
            Name = input.Name,
            IsEnabled = input.IsEnabled
        }, cancellationToken);
        if (entity is null)
            throw CreateEntityNotFoundException(id);

        return MapToDto(entity);
    }

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await _providerAccountService.DeleteAsync(id, cancellationToken);
        if (!deleted)
            throw CreateEntityNotFoundException(id);
    }

    public async Task<GetConfigurationResult<JsonElement>> LoadConfigurationAsync(Guid id, CancellationToken ct = default)
    {
        var providerAccount = await Repository.GetByIdAsync(id, ct);
        if (providerAccount == null)
            throw CreateEntityNotFoundException(id);

        return await _providerAccountConfigurationService.LoadConfigurationAsync(providerAccount, ct);
    }

    public async Task SaveConfigurationAsync(Guid id, JsonElement configuration, CancellationToken ct = default)
    {
        var providerAccount = await Repository.GetByIdAsync(id, ct);
        if (providerAccount == null)
            throw CreateEntityNotFoundException(id);

        await _providerAccountConfigurationService.SaveConfigurationAsync(providerAccount, configuration, ct);
    }

    public async Task<List<ProviderModelDto>> ListModelsAsync(Guid id, CancellationToken ct = default)
    {
        var providerAccount = await Repository.GetByIdAsync(id, ct);
        if (providerAccount == null)
            throw new KeyNotFoundException($"Provider account '{id}' was not found.");
        using var disposable=GetRequiredService<ICurrentProviderDetail>().Use(new ProviderDetail(providerAccount.Id.ToString()));
        if (PlatformRegistry.TryGetPlatform(providerAccount.ProtocolType, out var platform)&& platform is IHasProtocol hasProtocol) {
            var protocol=hasProtocol.GetProtocol();
            await TryInitializeAsync(protocol, id, ct);
            // 使用反射处理未知泛型的 IProtocolWithEnvironment<T>
            var protocolType = protocol.GetType();
            var envInterface = protocolType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IProtocolWithEnvironment<>));
            if (envInterface != null)
            {
                // 查找 LoadConfigurationAsync 方法并调用
                var loadMethod = envInterface.GetMethod("LoadConfigurationAsync", new Type[] { typeof(string), typeof(CancellationToken) })
                                 ?? envInterface.GetMethod("LoadConfigurationAsync");
                var initializeMethod = envInterface.GetMethod("Initialize", new Type[] { envInterface.GetGenericArguments()[0] });

                if (loadMethod != null && initializeMethod != null)
                {
                    var loadTaskObj = loadMethod.Invoke(protocol, new object[] { id.ToString(), ct });
                    if (loadTaskObj is Task loadTask)
                    {
                        await loadTask.ConfigureAwait(false);

                        // 从 Task<TResult>.Result 获取 Configuration
                        var resultProperty = loadTaskObj.GetType().GetProperty("Result");
                        if (resultProperty != null)
                        {
                            var configurationResult = resultProperty.GetValue(loadTaskObj);
                            if (configurationResult != null)
                            {
                                var configurationProperty = configurationResult.GetType().GetProperty("Configuration");
                                if (configurationProperty != null)
                                {
                                    var configurationValue = configurationProperty.GetValue(configurationResult);
                                    // 调用 Initialize
                                    initializeMethod.Invoke(protocol, new object[] { configurationValue });
                                }
                            }
                        }
                    }
                }
            }

            var models =await protocol.ModelsAsync(ct);
            return models.Select(m => new ProviderModelDto
            {
                Id = m.ModelSlug,
                Vendor = m.VendorSlug,
                Protocols = string.Join(",", m.ModelProtocols)
            }).ToList();
        }
        return [];
    }

    private async Task TryInitializeAsync(IProtocol protocol, Guid id, CancellationToken ct)
    {
        // 使用反射处理未知泛型的 IProtocolWithEnvironment<T>
        var protocolType = protocol.GetType();
        var envInterface = protocolType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IProtocolWithEnvironment<>));
        if (envInterface != null)
        {
            // 查找 LoadConfigurationAsync 方法并调用
            var loadMethod = envInterface.GetMethod(nameof(IProtocolWithEnvironment<>.LoadConfigurationAsync), [typeof(string), typeof(CancellationToken)])
                             ?? envInterface.GetMethod(nameof(IProtocolWithEnvironment<>.LoadConfigurationAsync));
            var initializeMethod = envInterface.GetMethod(nameof(IProtocolWithEnvironment<>.Initialize), [envInterface.GetGenericArguments()[0]]);

            if (loadMethod != null && initializeMethod != null)
            {
                var loadTaskObj = loadMethod.Invoke(protocol, [id.ToString(), ct]);
                if (loadTaskObj is Task loadTask)
                {
                    await loadTask.ConfigureAwait(false);

                    // 从 Task<TResult>.Result 获取 Configuration
                    var resultProperty = loadTaskObj.GetType().GetProperty("Result");
                    if (resultProperty != null)
                    {
                        var configurationResult = resultProperty.GetValue(loadTaskObj);
                        if (configurationResult != null)
                        {
                            var configurationProperty = configurationResult.GetType().GetProperty("Configuration");
                            if (configurationProperty != null)
                            {
                                var configurationValue = configurationProperty.GetValue(configurationResult);
                                // 调用 Initialize
                                initializeMethod.Invoke(protocol, [configurationValue]);
                            }
                        }
                    }
                }
            }
        }

    }
}



