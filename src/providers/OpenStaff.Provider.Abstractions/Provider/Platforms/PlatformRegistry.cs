using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenHub.Agents;
using OpenStaff.Agent;
using OpenStaff.Provider.Protocols;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenStaff.Provider.Platforms;

/// <summary>
/// zh-CN: 平台仅声明平台键，具体能力由可选能力接口独立暴露。
/// en: A platform only declares its platform key; optional capability interfaces expose concrete capabilities independently.
/// </summary>
public interface IPlatform
{
    string PlatformKey { get; }
}

/// <summary>
/// zh-CN: 平台聊天客户端工厂 capability，负责按需创建 provider 级聊天客户端工厂。
/// en: Platform chat-client-factory capability that creates provider chat-client factories on demand.
/// </summary>
public interface IPlatformChatClientFactory
{
    Type FactoryType { get; }
    Type? EnvironmentType { get; }
    object CreateFactory(IServiceProvider serviceProvider, string envConfigJson);
}

/// <summary>
/// zh-CN: 平台任务智能体工厂 capability，负责按需解析真实的任务智能体工厂服务。
/// en: Platform task-agent-factory capability that resolves the real task-agent factory service on demand.
/// </summary>
public interface IPlatformTaskAgentFactory
{
    Type FactoryType { get; }
    ITaskAgentFactory ResolveFactory(IServiceProvider serviceProvider);
}

/// <summary>
/// zh-CN: 基于工厂类型的默认聊天客户端 capability 实现。
/// en: Default chat-client-factory capability implementation backed by a provider factory type.
/// </summary>
public sealed class PlatformChatClientFactoryCapability(Type factoryType, Type? environmentType = null) : IPlatformChatClientFactory
{
    public Type FactoryType { get; } = factoryType ?? throw new ArgumentNullException(nameof(factoryType));

    public Type? EnvironmentType { get; } = environmentType;

    public object CreateFactory(IServiceProvider serviceProvider, string envConfigJson)
    {
        if (EnvironmentType is null)
            return ActivatorUtilities.CreateInstance(serviceProvider, FactoryType);

        var env = PlatformCapabilityEnvironmentSerializer.DeserializeEnvironment(EnvironmentType, envConfigJson);
        return ActivatorUtilities.CreateInstance(serviceProvider, FactoryType, env);
    }
}

/// <summary>
/// zh-CN: 基于服务类型的默认任务智能体 capability 实现。
/// en: Default task-agent capability implementation backed by a registered service type.
/// </summary>
public sealed class PlatformTaskAgentFactoryCapability(Type factoryType) : IPlatformTaskAgentFactory
{
    public Type FactoryType { get; } = factoryType ?? throw new ArgumentNullException(nameof(factoryType));

    public ITaskAgentFactory ResolveFactory(IServiceProvider serviceProvider)
    {
        var factory = serviceProvider.GetRequiredService(FactoryType);
        return factory as ITaskAgentFactory
               ?? throw new InvalidOperationException(
                   $"Platform task-agent capability '{FactoryType.FullName}' did not resolve a valid {typeof(ITaskAgentFactory).FullName} instance.");
    }
}

/// <summary>
/// zh-CN: 平台直接暴露协议对象。
/// en: Exposes the protocol object directly from the platform.
/// </summary>
public interface IHasProtocol
{
    IProtocol GetProtocol();
}

/// <summary>
/// zh-CN: 平台直接暴露聊天客户端工厂 capability 对象。
/// en: Exposes the chat-client-factory capability object directly from the platform.
/// </summary>
public interface IHasChatClientFactory
{
    IChatClientFactory GetChatClientFactory();
}

/// <summary>
/// zh-CN: 平台直接暴露任务智能体工厂 capability 对象。
/// en: Exposes the task-agent factory capability object directly from the platform.
/// </summary>
public interface IHasTaskAgentFactory
{
    IPlatformTaskAgentFactory GetTaskAgentFactory();
}

/// <summary>
/// zh-CN: 平台直接暴露 Vendor 元数据服务。
/// en: Exposes the platform's vendor metadata service directly.
/// </summary>
public interface IHasVendorMetadata
{
    IVendorPlatformMetadata GetVendorMetadataService();
}

/// <summary>
/// zh-CN: 平台直接暴露 Vendor 模型目录服务。
/// en: Exposes the platform's vendor model-catalog service directly.
/// </summary>
public interface IHasModelCatalog
{
    IVendorModelCatalogService GetModelCatalogService();
}

/// <summary>
/// zh-CN: 平台直接暴露 Vendor 配置服务。
/// en: Exposes the platform's vendor configuration service directly.
/// </summary>
public interface IHasConfiguration
{
    IVendorConfigurationService GetConfigurationService();
}

public interface ITaskAgentFactory
{
    Task<ITaskAgent> CreateAsync(TaskAgentCreateRequest request, CancellationToken cancellationToken = default);
}

public sealed record TaskAgentRole(
    Guid Id,
    string Name,
    string? Description,
    string? JobTitle,
    string? ProviderType,
    Guid? ModelProviderId,
    string? ModelName,
    string? SystemPrompt,
    string? Config);

public sealed record TaskAgentContext(
    Guid? ProjectId,
    Guid? SessionId,
    Guid AgentInstanceId,
    string? ProjectName,
    string? WorkspacePath,
    string? Scene,
    IReadOnlyList<string> ResolvedSkillDirectories);

public sealed record TaskAgentCreateRequest(
    TaskAgentRole Role,
    TaskAgentContext Context,
    object? RawRole = null,
    object? RawContext = null);

public interface IPlatformRegistry
{
    IReadOnlyDictionary<string, IPlatform> Platforms { get; }
    bool TryGetPlatform(string platformKey, out IPlatform platform);
    IReadOnlyList<IProtocol> GetProtocols();
}

public sealed class PlatformRegistry(IEnumerable<IPlatform> platforms) : IPlatformRegistry
{
    private readonly Dictionary<string, IPlatform> _platforms = platforms
        .GroupBy(platform => platform.PlatformKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<IProtocol>? _protocols;

    public IReadOnlyDictionary<string, IPlatform> Platforms => _platforms;

    public bool TryGetPlatform(string platformKey, out IPlatform platform)
        => _platforms.TryGetValue(platformKey, out platform!);

    public IReadOnlyList<IProtocol> GetProtocols()
        => _protocols ??= _platforms.Values
            .OfType<IHasProtocol>()
            .Select(platform => platform.GetProtocol())
            .GroupBy(protocol => protocol.GetType())
            .Select(group => group.Last())
            .ToList();
}

internal static class PlatformCapabilityEnvironmentSerializer
{
    internal static object DeserializeEnvironment(Type environmentType, string envConfigJson)
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
