using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Options;
using OpenStaff.Provider.Platforms;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenStaff.Provider.Protocols;

/// <summary>
/// 协议工厂接口，用于发现协议、读取元数据并按配置实例化协议。
/// Protocol factory contract used to discover protocols, inspect metadata, and instantiate configured protocols.
/// </summary>
public interface IProtocolFactory
{
    /// <summary>
    /// 获取所有已注册的协议实例。
    /// Gets instances for all registered protocols.
    /// </summary>
    /// <returns>
    /// 已注册协议的实例集合。
    /// A collection of registered protocol instances.
    /// </returns>
    IEnumerable<IProtocol> AllProtocols();

    /// <summary>
    /// 获取所有已注册协议的元数据。
    /// Gets metadata for all registered protocols.
    /// </summary>
    /// <returns>
    /// 协议元数据列表。
    /// List of protocol metadata entries.
    /// </returns>
    IReadOnlyList<ProtocolMetadata> GetProtocolMetadata();

    /// <summary>
    /// 解析指定协议对应的环境配置类型。
    /// Resolves the environment type associated with the specified protocol.
    /// </summary>
    /// <param name="protocolName">
    /// 协议键。
    /// Protocol key.
    /// </param>
    /// <returns>
    /// 协议环境配置类型；未找到时返回 <see langword="null" />。
    /// Protocol environment type, or <see langword="null" /> when no matching protocol is found.
    /// </returns>
    Type? GetProtocolEnvType(string protocolName);

    /// <summary>
    /// 使用 JSON 环境配置创建协议实例。
    /// Creates a protocol instance using JSON environment settings.
    /// </summary>
    /// <param name="protocolName">
    /// 协议键。
    /// Protocol key.
    /// </param>
    /// <param name="envConfigJson">
    /// 协议环境配置 JSON。
    /// Protocol environment JSON.
    /// </param>
    /// <returns>
    /// 已初始化的协议实例。
    /// Initialized protocol instance.
    /// </returns>
    IProtocol CreateProtocolWithEnv(string protocolName, string envConfigJson);

    /// <summary>
    /// 创建指定协议类型的实例。
    /// Creates an instance of the specified protocol type.
    /// </summary>
    /// <typeparam name="TProtocol">
    /// 协议类型。
    /// Protocol type.
    /// </typeparam>
    /// <returns>
    /// 协议实例。
    /// Protocol instance.
    /// </returns>
    TProtocol CreateProtocol<TProtocol>() where TProtocol : IProtocol;

    /// <summary>
    /// 使用指定环境配置创建协议实例。
    /// Creates a protocol instance using the specified environment settings.
    /// </summary>
    /// <typeparam name="TProtocol">
    /// 协议类型。
    /// Protocol type.
    /// </typeparam>
    /// <typeparam name="TProtocolEnv">
    /// 协议环境配置类型。
    /// Protocol environment type.
    /// </typeparam>
    /// <param name="env">
    /// 协议环境配置。
    /// Protocol environment settings.
    /// </param>
    /// <returns>
    /// 已初始化的协议实例。
    /// Initialized protocol instance.
    /// </returns>
    TProtocol CreateProtocol<TProtocol, TProtocolEnv>(TProtocolEnv env)
        where TProtocolEnv : ProtocolEnvBase
        where TProtocol : IProtocol, IProtocolWithEnvironment<TProtocolEnv>;
}

/// <summary>
/// 默认协议工厂，负责按注册表创建协议实例并生成配置元数据。
/// Default protocol factory that creates protocol instances from the registry and generates configuration metadata.
/// </summary>
internal class ProtocolFactory : IProtocolFactory
{
    private List<ProtocolMetadata>? _metadataCache;
    private readonly IPlatformRegistry? _platformRegistry;

    /// <summary>
    /// 初始化协议工厂。
    /// Initializes the protocol factory.
    /// </summary>
    /// <param name="serviceProvider">
    /// 用于激活协议实例和其依赖项的服务提供程序。
    /// Service provider used to activate protocol instances and their dependencies.
    /// </param>
    /// <param name="providerOptions">
    /// 保存已注册协议类型列表的选项对象。
    /// Options object that stores the list of registered protocol types.
    /// </param>
    public ProtocolFactory(IServiceProvider serviceProvider, IOptions<ProviderOptions> providerOptions)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ServiceProvider = serviceProvider;
        ProviderOptions = providerOptions;
        _platformRegistry = serviceProvider.GetService<IPlatformRegistry>();
    }

    protected IServiceProvider ServiceProvider { get; }

    protected IOptions<ProviderOptions> ProviderOptions { get; }

    private IEnumerable<IProtocol> EnumerateProtocols()
    {
        var seen = new HashSet<Type>();

        if (_platformRegistry != null)
        {
            foreach (var protocol in _platformRegistry.GetProtocols())
            {
                if (seen.Add(protocol.GetType()))
                    yield return protocol;
            }
        }

        foreach (var protocolType in ProviderOptions.Value.Protocols)
        {
            if (!typeof(IProtocol).IsAssignableFrom(protocolType))
                throw new InvalidOperationException($"Type {protocolType.FullName} does not implement IProtocol.");
            if (seen.Add(protocolType))
                yield return (IProtocol)ActivatorUtilities.CreateInstance(ServiceProvider, protocolType);
        }
    }

    /// <summary>
    /// 创建并初始化需要环境配置的协议实例，适用于调用方已知强类型环境对象的场景。
    /// Creates and initializes a protocol instance that requires environment settings when the caller already has a strongly typed environment object.
    /// </summary>
    /// <typeparam name="TProtocol">
    /// 要创建的协议类型。
    /// Protocol type to create.
    /// </typeparam>
    /// <typeparam name="TProtocolEnv">
    /// 协议环境配置类型。
    /// Protocol environment type.
    /// </typeparam>
    /// <param name="env">
    /// 已解析完成的协议环境配置。
    /// Fully parsed protocol environment settings.
    /// </param>
    /// <returns>
    /// 已完成初始化的协议实例。
    /// Initialized protocol instance.
    /// </returns>
    public TProtocol CreateProtocol<TProtocol, TProtocolEnv>(TProtocolEnv env)
        where TProtocol : IProtocol, IProtocolWithEnvironment<TProtocolEnv>
        where TProtocolEnv : ProtocolEnvBase
    {
        var protocol = ActivatorUtilities.CreateInstance<TProtocol>(ServiceProvider);
        protocol.Initialize(env);
        return protocol;
    }

    /// <summary>
    /// 仅通过依赖注入创建协议实例，不附带任何环境配置。
    /// Creates a protocol instance through dependency injection only, without attaching any environment settings.
    /// </summary>
    /// <typeparam name="TProtocol">
    /// 要创建的协议类型。
    /// Protocol type to create.
    /// </typeparam>
    /// <returns>
    /// 尚未初始化环境配置的协议实例。
    /// Protocol instance whose environment has not yet been initialized.
    /// </returns>
    public virtual TProtocol CreateProtocol<TProtocol>() where TProtocol : IProtocol
    {
        return ActivatorUtilities.CreateInstance<TProtocol>(ServiceProvider);
    }

    /// <summary>
    /// 实例化所有已注册协议，供协议列表展示、验证或批量探测流程使用。
    /// Instantiates all registered protocols for protocol listings, validation, or batch discovery workflows.
    /// </summary>
    /// <returns>
    /// 当前注册表中的协议实例集合。
    /// Collection of protocol instances from the current registry.
    /// </returns>
    public virtual IEnumerable<IProtocol> AllProtocols()
    {
        var protocols = new List<IProtocol>();
        foreach (var protocol in EnumerateProtocols())
            protocols.Add(protocol);

        return protocols;
    }

    /// <summary>
    /// 生成所有已注册协议的元数据快照，并缓存环境架构结果以降低重复反射开销。
    /// Generates a metadata snapshot for every registered protocol and caches the environment schema to reduce repeated reflection overhead.
    /// </summary>
    /// <returns>
    /// 可供管理界面或 API 返回的协议元数据列表。
    /// Protocol metadata list suitable for management UIs or API responses.
    /// </returns>
    public IReadOnlyList<ProtocolMetadata> GetProtocolMetadata()
    {
        if (_metadataCache != null) return _metadataCache;

        var result = new List<ProtocolMetadata>();

        foreach (var protocol in EnumerateProtocols())
        {
            var protocolType = protocol.GetType();
            var envType = ResolveEnvType(protocolType);
            var schema = envType != null ? BuildEnvSchema(envType) : [];

            result.Add(new ProtocolMetadata(
                protocol.ProtocolKey,
                protocol.ProtocolName,
                protocol.Logo,
                protocol.IsVendor,
                protocolType.Name,
                schema));
        }

        _metadataCache = result;
        return result;
    }

    /// <summary>
    /// 根据协议键解析其环境配置类型，供配置界面动态构建表单或校验逻辑使用。
    /// Resolves the environment type for a protocol key so configuration UIs and validation flows can build the correct schema dynamically.
    /// </summary>
    /// <param name="protocolName">
    /// 协议唯一键。
    /// Unique protocol key.
    /// </param>
    /// <returns>
    /// 对应的环境配置类型；未找到时返回 <see langword="null" />。
    /// Matching environment type, or <see langword="null" /> when no protocol matches.
    /// </returns>
    public Type? GetProtocolEnvType(string protocolName)
    {
        foreach (var protocol in EnumerateProtocols())
        {
            if (string.Equals(protocol.ProtocolKey, protocolName, StringComparison.OrdinalIgnoreCase))
                return ResolveEnvType(protocol.GetType());
        }

        return null;
    }

    /// <summary>
    /// 根据协议键和 JSON 配置动态创建协议实例，适用于运行时才知道目标 provider 的场景。
    /// Dynamically creates a protocol instance from a protocol key and JSON configuration for scenarios where the target provider is only known at runtime.
    /// </summary>
    /// <param name="protocolName">
    /// 要创建的协议键。
    /// Protocol key to instantiate.
    /// </param>
    /// <param name="envConfigJson">
    /// 协议环境配置 JSON。
    /// Protocol environment JSON.
    /// </param>
    /// <returns>
    /// 根据输入配置完成初始化的协议实例。
    /// Protocol instance initialized from the supplied configuration.
    /// </returns>
    public IProtocol CreateProtocolWithEnv(string protocolName, string envConfigJson)
    {
        foreach (var tempProtocol in EnumerateProtocols())
        {
            if (!string.Equals(tempProtocol.ProtocolKey, protocolName, StringComparison.OrdinalIgnoreCase))
                continue;

            var protocolType = tempProtocol.GetType();
            var protocol = (IProtocol)ActivatorUtilities.CreateInstance(ServiceProvider, protocolType);
            var envType = ResolveEnvType(protocolType);
            if (envType is null)
                return protocol;

            var env = DeserializeEnvironment(envType, envConfigJson);
            var initMethod = protocolType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
            initMethod?.Invoke(protocol, [env]);
            return protocol;
        }

        throw new InvalidOperationException($"Protocol '{protocolName}' not found.");
    }

    private static Type? ResolveEnvType(Type protocolType)
    {
        foreach (var iface in protocolType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IProtocolWithEnvironment<>))
                return iface.GetGenericArguments()[0];
        }

        var baseType = protocolType.BaseType;
        while (baseType != null)
        {
            foreach (var iface in baseType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IProtocolWithEnvironment<>))
                    return iface.GetGenericArguments()[0];
            }

            baseType = baseType.BaseType;
        }

        return null;
    }

    private static object DeserializeEnvironment(Type environmentType, string envConfigJson)
    {
        var env = JsonSerializer.Deserialize(
            string.IsNullOrWhiteSpace(envConfigJson) ? "{}" : envConfigJson,
            environmentType,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new LenientBoolConverter() }
            });

        return env
               ?? Activator.CreateInstance(environmentType)
               ?? throw new InvalidOperationException($"Failed to create protocol environment '{environmentType.FullName}'.");
    }

    /// <summary>
    /// 通过反射构建协议环境字段架构，以便前端或 API 能动态展示可配置项。
    /// Builds the protocol environment field schema via reflection so the frontend or API can display configurable fields dynamically.
    /// </summary>
    /// <param name="envType">
    /// 协议环境配置类型。
    /// Protocol environment type.
    /// </param>
    /// <returns>
    /// 字段名称、类型和默认值组成的架构列表。
    /// Schema list containing field names, field types, and default values.
    /// </returns>
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
                defaultValue?.ToString() ?? string.Empty));
        }

        return fields;
    }

    /// <summary>
    /// 将环境属性的 CLR 类型转换为配置界面使用的字段类型标记。
    /// Converts an environment property's CLR type into the field-type token used by configuration UIs.
    /// </summary>
    /// <param name="prop">
    /// 要转换的属性元数据。
    /// Property metadata to convert.
    /// </param>
    /// <returns>
    /// 对应的字段类型标记，例如 string、secret、bool 或 number。
    /// Matching field-type token such as string, secret, bool, or number.
    /// </returns>
    private static string ResolveFieldType(PropertyInfo prop)
    {
        // zh-CN: [Encrypted] 优先映射为 secret，以便 UI 和配置端把该字段视作敏感信息。
        // en: [Encrypted] takes precedence and maps to secret so UI and configuration flows treat the field as sensitive.
        if (prop.GetCustomAttribute<EncryptedAttribute>() != null)
            return "secret";

        var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        if (type == typeof(bool)) return "bool";
        if (type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(decimal))
            return "number";

        return "string";
    }

    private sealed class LenientBoolConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.String => bool.TryParse(reader.GetString(), out var value) && value,
                JsonTokenType.Number => reader.GetInt32() != 0,
                _ => false
            };

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
            => writer.WriteBooleanValue(value);
    }
}
