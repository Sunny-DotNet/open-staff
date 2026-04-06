using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Options;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenStaff.Provider.Protocols;

public interface IProtocolFactory
{
    IEnumerable<IProtocol> AllProtocols();
    IReadOnlyList<ProtocolMetadata> GetProtocolMetadata();
    Type? GetProtocolEnvType(string protocolName);
    IProtocol CreateProtocolWithEnv(string protocolName, string envConfigJson);
    TProtocol CreateProtocol<TProtocol>() where TProtocol : IProtocol;
    TProtocol CreateProtocol<TProtocol, TProtocolEnv>(TProtocolEnv env)
        where TProtocolEnv : ProtocolEnvBase
        where TProtocol : IProtocol, IProtocolMustEnv<TProtocolEnv>;
}

internal class ProtocolFactory : IProtocolFactory
{
    protected IServiceProvider ServiceProvider { get; }
    protected IOptions<ProviderOptions> ProviderOptions { get; }

    private List<ProtocolMetadata>? _metadataCache;

    public ProtocolFactory(IServiceProvider serviceProvider, IOptions<ProviderOptions> providerOptions)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ServiceProvider = serviceProvider;
        ProviderOptions = providerOptions;
    }

    public TProtocol CreateProtocol<TProtocol, TProtocolEnv>(TProtocolEnv env)
        where TProtocol : IProtocol, IProtocolMustEnv<TProtocolEnv>
        where TProtocolEnv : ProtocolEnvBase
    {
        var protocol = ActivatorUtilities.CreateInstance<TProtocol>(ServiceProvider);
        protocol.Initialize(env);
        return protocol;
    }

    public virtual TProtocol CreateProtocol<TProtocol>() where TProtocol : IProtocol
    {
        return ActivatorUtilities.CreateInstance<TProtocol>(ServiceProvider);
    }

    public virtual IEnumerable<IProtocol> AllProtocols()
    {
        var protocols = new List<IProtocol>();
        foreach (var protocolType in ProviderOptions.Value.Protocols)
        {
            if (!typeof(IProtocol).IsAssignableFrom(protocolType))
                throw new InvalidOperationException($"Type {protocolType.FullName} does not implement IProtocol.");

            var protocol = (IProtocol)ActivatorUtilities.CreateInstance(ServiceProvider, protocolType);
            protocols.Add(protocol);
        }
        return protocols;
    }

    public IReadOnlyList<ProtocolMetadata> GetProtocolMetadata()
    {
        if (_metadataCache != null) return _metadataCache;

        var result = new List<ProtocolMetadata>();

        foreach (var protocolType in ProviderOptions.Value.Protocols)
        {
            var protocol = (IProtocol)ActivatorUtilities.CreateInstance(ServiceProvider, protocolType);
            var envType = ResolveEnvType(protocolType);
            var schema = envType != null ? BuildEnvSchema(envType) : [];

            result.Add(new ProtocolMetadata(
                protocol.ProviderKey,
                protocol.ProviderName,
                protocol.Logo,
                protocol.IsVendor,
                protocolType.Name,
                schema));
        }

        _metadataCache = result;
        return result;
    }

    public Type? GetProtocolEnvType(string protocolName)
    {
        foreach (var protocolType in ProviderOptions.Value.Protocols)
        {
            var protocol = (IProtocol)ActivatorUtilities.CreateInstance(ServiceProvider, protocolType);
            if (string.Equals(protocol.ProviderKey, protocolName, StringComparison.OrdinalIgnoreCase))
                return ResolveEnvType(protocolType);
        }
        return null;
    }

    public IProtocol CreateProtocolWithEnv(string protocolName, string envConfigJson)
    {
        foreach (var protocolType in ProviderOptions.Value.Protocols)
        {
            var tempProtocol = (IProtocol)ActivatorUtilities.CreateInstance(ServiceProvider, protocolType);
            if (!string.Equals(tempProtocol.ProviderKey, protocolName, StringComparison.OrdinalIgnoreCase))
                continue;

            var envType = ResolveEnvType(protocolType);
            if (envType == null) return tempProtocol;

            var env = (ProtocolEnvBase?)JsonSerializer.Deserialize(envConfigJson, envType,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new LenientBoolConverter() }
                });
            if (env == null) return tempProtocol;

            // 调用 Initialize(env) 通过反射
            var initMethod = protocolType.GetMethod("Initialize");
            initMethod?.Invoke(tempProtocol, [env]);

            return tempProtocol;
        }

        throw new InvalidOperationException($"Protocol '{protocolName}' not found.");
    }

    // ===== Private helpers =====

    private static Type? ResolveEnvType(Type protocolType)
    {
        // 遍历接口找 IProtocolMustEnv<TProtocolEnv> 的泛型参数
        foreach (var iface in protocolType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IProtocolMustEnv<>))
                return iface.GetGenericArguments()[0];
        }

        // 也检查基类链（ProtocolBase<T> 实现了 IProtocolMustEnv<T>）
        var baseType = protocolType.BaseType;
        while (baseType != null)
        {
            foreach (var iface in baseType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IProtocolMustEnv<>))
                    return iface.GetGenericArguments()[0];
            }
            baseType = baseType.BaseType;
        }

        return null;
    }

    private static readonly HashSet<string> SecretKeywords =
        new(StringComparer.OrdinalIgnoreCase) { "key", "secret", "password", "token" };

    private static List<ProtocolEnvField> BuildEnvSchema(Type envType)
    {
        var fields = new List<ProtocolEnvField>();
        var instance = Activator.CreateInstance(envType);

        foreach (var prop in envType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite) continue;

            var fieldType = ResolveFieldType(prop);
            var defaultValue = instance != null ? prop.GetValue(instance) : null;

            fields.Add(new ProtocolEnvField(
                prop.Name,
                fieldType,
                defaultValue?.ToString() ?? ""));
        }

        return fields;
    }

    private static string ResolveFieldType(PropertyInfo prop)
    {
        // 属性名含敏感关键词 → secret
        if (SecretKeywords.Any(kw => prop.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            return "secret";

        var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        if (type == typeof(bool)) return "bool";
        if (type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(decimal))
            return "number";

        return "string";
    }
}

/// <summary>
/// 宽松的 bool 转换器，支持 "true"/"false" 字符串
/// </summary>
internal class LenientBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => bool.TryParse(reader.GetString(), out var b) && b,
            JsonTokenType.Number => reader.GetInt32() != 0,
            _ => false
        };
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        => writer.WriteBooleanValue(value);
}

