using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Provider;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;
using OpenStaff.Repositories;
using System.Collections.Concurrent;

namespace OpenStaff.Agent.Builtin;

/// <summary>
/// zh-CN: 按 provider/protocol 类型把聊天客户端创建请求分发给平台声明的 provider factory。
/// en: Dispatches chat-client creation requests to the platform-declared provider factory by provider/protocol type.
/// </summary>
public class ChatClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IProviderAccountRepository? _providerAccounts;
    private readonly ICurrentProviderDetail? _currentProviderDetail;
    private readonly IProtocolFactory _protocolFactory;
    private readonly IPlatformRegistry _platformRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, Task<IReadOnlyDictionary<string, ModelInfo>>> _modelCache = new(StringComparer.Ordinal);

    /// <summary>
    /// zh-CN: 使用日志工厂、协议工厂、平台能力注册表和当前容器初始化聊天客户端分发器。
    /// en: Initializes the chat-client dispatcher with a logger factory, protocol factory, platform capability registry, and the current service container.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public ChatClientFactory(
        ILoggerFactory loggerFactory,
        IProtocolFactory protocolFactory,
        IPlatformRegistry platformRegistry,
        IServiceProvider serviceProvider)
    {
        _loggerFactory = loggerFactory;
        _protocolFactory = protocolFactory;
        _platformRegistry = platformRegistry;
        _serviceProvider = serviceProvider;
    }

    public ChatClientFactory(
        ILoggerFactory loggerFactory,
        IProviderAccountRepository providerAccounts,
        ICurrentProviderDetail currentProviderDetail,
        IProtocolFactory protocolFactory,
        IPlatformRegistry platformRegistry,
        IServiceProvider serviceProvider)
        : this(
            loggerFactory,
            protocolFactory,
            platformRegistry,
            serviceProvider)
    {
        _providerAccounts = providerAccounts;
        _currentProviderDetail = currentProviderDetail;
    }

    /// <summary>
    /// zh-CN: 基于角色显式绑定的账号标识创建聊天客户端，并把真正的创建逻辑委托给平台声明的独立 provider factory。
    /// en: Creates a chat client from the role-bound provider account id and delegates the actual construction to the standalone provider factory declared by the platform.
    /// </summary>
    public async Task<IChatClient> CreateAsync(
        Guid accountId,
        string model,
        CancellationToken cancellationToken = default)
    {
        return await WithProviderServicesAsync(
            async (providerAccounts, currentProviderDetail) =>
            {
                var account = await providerAccounts.FindAsync(accountId)
                    ?? throw new InvalidOperationException($"Provider account '{accountId}' was not found.");
                if (string.IsNullOrWhiteSpace(model))
                    throw new InvalidOperationException("Model is required to create a chat client.");

                var logger = _loggerFactory.CreateLogger<ChatClientFactory>();
                logger.LogInformation(
                    "Dispatching IChatClient creation for protocol {Protocol}, model={Model}",
                    account.ProtocolType,
                    model);

                if (!_platformRegistry.TryGetPlatform(account.ProtocolType, out var platform)
                    || platform is not IHasProtocol protocolProvider)
                {
                    throw new InvalidOperationException($"Platform '{account.ProtocolType}' is not registered.");
                }

                var typedProtocol = protocolProvider.GetProtocol();
                using var providerScope = currentProviderDetail.Use(new ProviderDetail(account.Id.ToString()));
                var envConfigJson = await LoadProtocolEnvironmentJsonAsync(typedProtocol, account.Id, cancellationToken);
                var modelInfo = await ResolveModelInfoAsync(account, typedProtocol, model, cancellationToken);
                var chatClientFactory = ResolveProviderChatClientFactory(account.ProtocolType, envConfigJson);
                return await chatClientFactory.CreateAsync(new ChatClientCreateRequest(account.Id.ToString(), modelInfo.ModelSlug), cancellationToken);
            });
    }

    public async Task<IChatClient> CreateAsync(
        ResolvedProvider provider,
        string model,
        CancellationToken cancellationToken = default)
    {
        return await WithCurrentProviderDetailAsync(
            async currentProviderDetail =>
            {
                var account = provider.Account
                    ?? throw new InvalidOperationException("Provider account is required to create a chat client.");
                if (string.IsNullOrWhiteSpace(model))
                    throw new InvalidOperationException("Model is required to create a chat client.");

                var protocol = _protocolFactory.CreateProtocolWithEnv(account.ProtocolType, provider.EnvConfigJson ?? "{}");
                using var providerScope = currentProviderDetail.Use(new ProviderDetail(account.Id.ToString()));
                var modelInfo = await ResolveModelInfoAsync(account, protocol, model, cancellationToken);
                var chatClientFactory = ResolveProviderChatClientFactory(account.ProtocolType, provider.EnvConfigJson ?? "{}");
                return await chatClientFactory.CreateAsync(new ChatClientCreateRequest(account.Id.ToString(), modelInfo.ModelSlug), cancellationToken);
            });
    }

    /// <summary>
    /// zh-CN: 基于账号更新时间缓存协议模型目录，并解析当前模型的元数据；找不到时返回一个仅包含模型标识的兜底条目。
    /// en: Caches the protocol model catalog by account update timestamp and resolves metadata for the current model, returning a fallback entry with only the model identifier when no match exists.
    /// </summary>
    internal async Task<ModelInfo> ResolveModelInfoAsync(
        Entities.ProviderAccount account,
        IProtocol protocol,
        string model,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("Model is required to resolve model info.");

        var cacheVersion = (account.UpdatedAt ?? account.CreatedAt).Ticks;
        var cacheKey = $"{account.ProtocolType}:{account.Id:N}:{cacheVersion}";
        var modelMapTask = _modelCache.GetOrAdd(cacheKey, _ => LoadModelInfoMapAsync(protocol));

        try
        {
            var modelMap = await modelMapTask.WaitAsync(cancellationToken);
            return modelMap.TryGetValue(model, out var modelInfo)
                ? modelInfo
                : new ModelInfo(model, protocol.ProtocolKey, ModelProtocolType.None);
        }
        catch
        {
            _modelCache.TryRemove(cacheKey, out _);
            throw;
        }
    }

    internal Task<ModelInfo> ResolveModelInfoAsync(
        ResolvedProvider provider,
        IProtocol protocol,
        string model,
        CancellationToken cancellationToken = default)
    {
        var account = provider.Account
            ?? throw new InvalidOperationException("Provider account is required to resolve model info.");
        return ResolveModelInfoAsync(account, protocol, model, cancellationToken);
    }

    /// <summary>
    /// zh-CN: 调用协议拉取模型目录，并折叠为按模型名索引的缓存映射。
    /// en: Fetches the model catalog from the protocol and folds it into a cache map keyed by model name.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, ModelInfo>> LoadModelInfoMapAsync(IProtocol protocol)
    {
        var models = await protocol.ModelsAsync();

        var result = new Dictionary<string, ModelInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in models)
        {
            if (string.IsNullOrWhiteSpace(item.ModelSlug))
                continue;

            result[item.ModelSlug] = item;
        }

        return result;
    }

    private IChatClientFactory ResolveProviderChatClientFactory(string protocolName, string envConfigJson)
    {
        if (!_platformRegistry.TryGetPlatform(protocolName, out var platform))
            throw new InvalidOperationException($"Platform '{protocolName}' is not registered.");
        if (platform is not IHasChatClientFactory chatClientFactoryPlatform)
            throw new InvalidOperationException($"Platform '{protocolName}' does not expose a runtime IChatClient factory capability.");
        return chatClientFactoryPlatform.GetChatClientFactory();
    }

    private static async Task<string> LoadProtocolEnvironmentJsonAsync(IProtocol protocol, Guid accountId, CancellationToken cancellationToken)
    {
        var loadMethod = protocol.GetType().GetMethod("LoadConfigurationAsync", [typeof(string), typeof(CancellationToken)]);
        if (loadMethod == null)
            return "{}";

        dynamic loadTask = loadMethod.Invoke(protocol, [accountId.ToString(), cancellationToken])
            ?? throw new InvalidOperationException($"Protocol '{protocol.GetType().FullName}' returned a null configuration task.");
        var configurationResult = await loadTask;
        var configuration = (object?)configurationResult.Configuration;
        var initializeMethod = protocol.GetType().GetMethod("Initialize", [configuration?.GetType() ?? typeof(object)]);
        initializeMethod?.Invoke(protocol, [configuration]);
        return System.Text.Json.JsonSerializer.Serialize(configuration ?? new { });
    }

    private async Task<T> WithProviderServicesAsync<T>(
        Func<IProviderAccountRepository, ICurrentProviderDetail, Task<T>> action)
    {
        if (_providerAccounts is not null && _currentProviderDetail is not null)
            return await action(_providerAccounts, _currentProviderDetail);

        using var scope = _serviceProvider.CreateScope();
        return await action(
            scope.ServiceProvider.GetRequiredService<IProviderAccountRepository>(),
            scope.ServiceProvider.GetRequiredService<ICurrentProviderDetail>());
    }

    private async Task<T> WithCurrentProviderDetailAsync<T>(Func<ICurrentProviderDetail, Task<T>> action)
    {
        if (_currentProviderDetail is not null)
            return await action(_currentProviderDetail);

        using var scope = _serviceProvider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ICurrentProviderDetail>());
    }

}
