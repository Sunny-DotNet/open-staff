using System.Reflection;
using System.Text.Json;
using OpenStaff.Configurations;
using OpenStaff.Entities;
using OpenStaff.Provider.Protocols;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Providers.Services;
/// <summary>
/// 提供商账户配置门面，负责在原始 JSON API 与协议强类型环境配置之间做适配。
/// Facade that adapts raw JSON provider-account APIs to strongly typed protocol environment configurations.
/// </summary>
public class ProviderAccountConfigurationService
{
    private const string ConfigurationPropertyName = "Configuration";
    private const string LoadConfigurationMethodName = "LoadConfigurationAsync";
    private const string PropertiesPropertyName = "Properties";
    private const string SaveConfigurationMethodName = "SaveConfigurationAsync";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly ProviderAccountService _providerAccountService;
    private readonly IProtocolFactory _protocolFactory;
    private readonly IRepositoryContext _repositoryContext;

    public ProviderAccountConfigurationService(
        ProviderAccountService providerAccountService,
        IProtocolFactory protocolFactory,
        IRepositoryContext repositoryContext)
    {
        _providerAccountService = providerAccountService;
        _protocolFactory = protocolFactory;
        _repositoryContext = repositoryContext;
    }

    public async Task<GetConfigurationResult<JsonElement>> LoadConfigurationAsync(ProviderAccount account, CancellationToken cancellationToken = default)
    {
        var protocol = ResolveConfigurationProtocol(account.ProtocolType, out var envType);
        var snapshot = await LoadTypedConfigurationAsync(protocol, account.Id.ToString(), envType, cancellationToken);
        return new(
            snapshot.Properties,
            JsonSerializer.SerializeToElement(ToConfigurationDictionary(snapshot.Configuration, includeEncrypted: false)));
    }

    public async Task SaveConfigurationAsync(ProviderAccount account, JsonElement configuration, CancellationToken cancellationToken = default)
    {
        if (configuration.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Provider account configuration payload must be a JSON object.", nameof(configuration));

        var protocol = ResolveConfigurationProtocol(account.ProtocolType, out var envType);
        var existingConfiguration = (await LoadTypedConfigurationAsync(protocol, account.Id.ToString(), envType, cancellationToken)).Configuration;
        var previousEnvConfig = await _providerAccountService.ReadRawEnvConfigAsync(account.Id, cancellationToken);
        var mergedConfiguration = MergeConfiguration(existingConfiguration, configuration.Clone(), envType);
        var succeeded = false;

        try
        {
            await SaveTypedConfigurationAsync(protocol, account.Id.ToString(), mergedConfiguration, cancellationToken);
            account.UpdatedAt = DateTime.UtcNow;
            await _repositoryContext.SaveChangesAsync(cancellationToken);
            succeeded = true;
        }
        finally
        {
            if (!succeeded)
                await _providerAccountService.RestoreRawEnvConfigAsync(account.Id, previousEnvConfig, cancellationToken);
        }
    }

    private IProtocol ResolveConfigurationProtocol(string protocolType, out Type envType)
    {
        var protocol = _protocolFactory
            .AllProtocols()
            .FirstOrDefault(candidate => string.Equals(candidate.ProtocolKey, protocolType, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Protocol '{protocolType}' was not found.");

        var configurationContract = FindConfigurationContract(protocol.GetType())
            ?? throw new InvalidOperationException($"Protocol '{protocolType}' does not support environment configuration.");

        envType = configurationContract.GetGenericArguments()[0];
        return protocol;
    }

    private static Type? FindConfigurationContract(Type protocolType)
        => protocolType
            .GetInterfaces()
            .FirstOrDefault(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IProtocolWithEnvironment<>));

    private static async Task<(ConfigurationProperty[] Properties, object Configuration)> LoadTypedConfigurationAsync(
        IProtocol protocol,
        string configurationId,
        Type envType,
        CancellationToken cancellationToken)
    {
        var configurationContract = FindConfigurationContract(protocol.GetType())
            ?? throw new InvalidOperationException($"Protocol '{protocol.ProtocolKey}' does not support environment configuration.");
        var loadMethod = configurationContract.GetMethod(LoadConfigurationMethodName)
            ?? throw new InvalidOperationException($"Protocol '{protocol.ProtocolKey}' does not expose '{LoadConfigurationMethodName}'.");
        var task = loadMethod.Invoke(protocol, [configurationId, cancellationToken]) as Task
            ?? throw new InvalidOperationException($"Protocol '{protocol.ProtocolKey}' returned an invalid '{LoadConfigurationMethodName}' task.");

        await task;

        var result = task.GetType().GetProperty("Result")?.GetValue(task)
            ?? throw new InvalidOperationException($"Protocol '{protocol.ProtocolKey}' returned no configuration result.");
        var properties = result.GetType().GetProperty(PropertiesPropertyName)?.GetValue(result) as ConfigurationProperty[] ?? [];
        var configuration = result.GetType().GetProperty(ConfigurationPropertyName)?.GetValue(result)
            ?? Activator.CreateInstance(envType)
            ?? throw new InvalidOperationException($"Could not create the default configuration instance for protocol '{protocol.ProtocolKey}'.");

        return (properties, configuration);
    }

    private static async Task SaveTypedConfigurationAsync(IProtocol protocol, string configurationId, object configuration, CancellationToken cancellationToken)
    {
        var configurationContract = FindConfigurationContract(protocol.GetType())
            ?? throw new InvalidOperationException($"Protocol '{protocol.ProtocolKey}' does not support environment configuration.");
        var saveMethod = configurationContract.GetMethod(SaveConfigurationMethodName)
            ?? throw new InvalidOperationException($"Protocol '{protocol.ProtocolKey}' does not expose '{SaveConfigurationMethodName}'.");
        var task = saveMethod.Invoke(protocol, [configurationId, configuration, cancellationToken]) as Task
            ?? throw new InvalidOperationException($"Protocol '{protocol.ProtocolKey}' returned an invalid '{SaveConfigurationMethodName}' task.");

        await task;
    }

    private static object MergeConfiguration(object existingConfiguration, JsonElement configuration, Type envType)
    {
        var merged = ToConfigurationDictionary(existingConfiguration, includeEncrypted: true);
        foreach (var pair in ToConfigurationDictionary(configuration))
            merged[pair.Key] = pair.Value;

        return JsonSerializer.Deserialize(JsonSerializer.Serialize(merged), envType, JsonOptions)
            ?? Activator.CreateInstance(envType)
            ?? throw new InvalidOperationException($"Could not deserialize provider account configuration as '{envType.Name}'.");
    }

    private static Dictionary<string, object?> ToConfigurationDictionary(object configuration, bool includeEncrypted)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var properties = configuration.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.CanWrite);

        foreach (var property in properties)
        {
            if (!includeEncrypted && property.GetCustomAttribute<EncryptedAttribute>() != null)
                continue;
            var name = JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name);
            result[name] = property.GetValue(configuration);
        }

        return result;
    }

    private static Dictionary<string, object?> ToConfigurationDictionary(JsonElement configuration)
    {
        if (configuration.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Provider account configuration payload must be a JSON object.", nameof(configuration));

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in configuration.EnumerateObject())
            result[JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name)] = ConvertJsonValue(property.Value);

        return result;
    }

    private static object? ConvertJsonValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Number => ConvertJsonNumber(value),
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            _ => throw new NotSupportedException("Provider account configuration only supports primitive JSON values.")
        };

    private static object ConvertJsonNumber(JsonElement value)
    {
        if (value.TryGetInt32(out var int32Value))
            return int32Value;

        if (value.TryGetInt64(out var int64Value))
            return int64Value;

        if (value.TryGetDecimal(out var decimalValue))
            return decimalValue;

        return value.GetDouble();
    }
}

