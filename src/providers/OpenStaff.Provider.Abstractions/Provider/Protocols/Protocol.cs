using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenStaff.Configurations;
using OpenStaff.Infrastructure.Security;
using OpenStaff.Options;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider.Models;

namespace OpenStaff.Provider.Protocols;

/// <summary>
/// 协议接口，定义模型发现所需的基础元数据与行为。
/// Protocol contract that exposes the metadata and behavior required for model discovery.
/// </summary>
public interface IProtocol
{
    /// <summary>
    /// 指示该协议是否直接代表一个模型供应商。
    /// Indicates whether the protocol directly represents a model vendor.
    /// </summary>
    bool IsVendor { get; }

    /// <summary>
    /// 协议唯一键，用于配置、查找与序列化。
    /// Unique protocol key used for configuration, lookup, and serialization.
    /// </summary>
    string ProtocolKey { get; }

    /// <summary>
    /// 协议显示名称。
    /// Human-readable protocol name.
    /// </summary>
    string ProtocolName { get; }

    /// <summary>
    /// 协议图标标识。
    /// Protocol logo identifier.
    /// </summary>
    string Logo { get; }

    /// <summary>
    /// 获取该协议可用的模型列表。
    /// Gets the list of models available through the protocol.
    /// </summary>
    /// <param name="cancellationToken">
    /// 取消操作的令牌。
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// 协议可用模型的枚举结果。
    /// An enumerable of models available to the protocol.
    /// </returns>
    Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default);

}

/// <summary>
/// 需要环境配置的协议初始化约定。
/// Initialization contract for protocols that require environment settings.
/// </summary>
/// <typeparam name="TProtocolEnv">
/// 协议环境配置类型。
/// Protocol environment type.
/// </typeparam>
public interface IProtocolWithEnvironment<TProtocolEnv> where TProtocolEnv : ProtocolEnvBase
{
    /// <summary>
    /// 使用指定环境配置初始化协议实例。
    /// Initializes the protocol instance with the specified environment settings.
    /// </summary>
    /// <param name="env">
    /// 协议环境配置。
    /// Protocol environment settings.
    /// </param>
    void Initialize(TProtocolEnv env);

    Task<GetConfigurationResult<TProtocolEnv>> LoadConfigurationAsync(string configurationId, CancellationToken cancellationToken = default);
    Task SaveConfigurationAsync(string configurationId, TProtocolEnv protocolEnv, CancellationToken cancellationToken = default);
}

/// <summary>
/// 协议基类，为协议实现提供日志、依赖服务和模型数据源访问。
/// Base class for protocols that provides logging, dependency access, and model data source integration.
/// </summary>
/// <typeparam name="TProtocolEnv">
/// 协议环境配置类型。
/// Protocol environment type.
/// </typeparam>
public abstract class ProtocolBase<TProtocolEnv> : ServiceBase,IProtocol, IProtocolWithEnvironment<TProtocolEnv>
    where TProtocolEnv : ProtocolEnvBase,new()
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 初始化协议基类。
    /// Initializes the protocol base class.
    /// </summary>
    /// <param name="serviceProvider">
    /// 用于解析协议依赖的服务提供程序。
    /// Service provider used to resolve protocol dependencies.
    /// </param>
    protected ProtocolBase(IServiceProvider serviceProvider):base(serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Logger = GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
        ModelDataSource = GetRequiredService<IModelDataSource>();
    }

    /// <inheritdoc />
    public abstract bool IsVendor { get; }

    /// <inheritdoc />
    public abstract string ProtocolKey { get; }

    /// <inheritdoc />
    public abstract string ProtocolName { get; }

    /// <inheritdoc />
    public abstract string Logo { get; }

    /// <summary>
    /// 当前协议的日志记录器。
    /// Logger associated with the current protocol.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// 共享的模型数据源。
    /// Shared model metadata source.
    /// </summary>
    protected IModelDataSource ModelDataSource { get; }

    /// <summary>
    /// 当前协议已经初始化的环境配置。
    /// Environment configuration currently applied to the protocol.
    /// </summary>
    protected virtual TProtocolEnv Env { get; private set; } = null!;

    private bool IsDevelopment => _serviceProvider.GetService<IHostEnvironment>()?.IsDevelopment() == true;
    private Func<string, string>? EncryptFunc
    {
        get
        {
            var encryption = _serviceProvider.GetService<EncryptionService>();
            return encryption == null || IsDevelopment ? null : encryption.Encrypt;
        }
    }

    private Func<string, string>? DecryptFunc
    {
        get
        {
            var encryption = _serviceProvider.GetService<EncryptionService>();
            return encryption == null || IsDevelopment ? null : encryption.Decrypt;
        }
    }

    /// <inheritdoc />
    public abstract Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存协议运行所需的环境配置，供后续模型发现或调用阶段复用。
    /// Stores the environment settings required for protocol execution so later discovery or invocation steps can reuse them.
    /// </summary>
    /// <param name="env">
    /// 已解析的协议环境配置。
    /// Parsed protocol environment settings.
    /// </param>
    public void Initialize(TProtocolEnv env)
    {
        Env = env;
    }


    public virtual ConfigurationProperty[] GetConfigurationProperties() => ConfigurationHelper.GetConfigurationProperty<TProtocolEnv>();

    public virtual async Task<GetConfigurationResult<TProtocolEnv>> LoadConfigurationAsync(string configurationId, CancellationToken cancellationToken = default)
    {
        var filename = GetConfigurationFilePath(configurationId);
        if (!File.Exists(filename))
            return new(GetConfigurationProperties(), new());

        var json = await File.ReadAllTextAsync(filename, cancellationToken);
        var protocolEnv = ProtocolEnvSerializer.Deserialize<TProtocolEnv>(json, DecryptFunc) ?? new();
        return new(GetConfigurationProperties(), protocolEnv);
    }
    public virtual async Task SaveConfigurationAsync(string configurationId, TProtocolEnv protocolEnv, CancellationToken cancellationToken = default)
    {
        var filename = GetConfigurationFilePath(configurationId);
        var directory = Path.GetDirectoryName(filename)
            ?? throw new InvalidOperationException($"Could not resolve configuration directory for '{configurationId}'.");
        Directory.CreateDirectory(directory);

        var json = ProtocolEnvSerializer.Serialize(protocolEnv, EncryptFunc);
        var tempFilePath = $"{filename}.tmp";
        await File.WriteAllTextAsync(tempFilePath, json, cancellationToken);
        File.Move(tempFilePath, filename, overwrite: true);
    }

    private string GetConfigurationFilePath(string configurationId)
        => Path.Combine(GetRequiredService<IOptions<OpenStaffOptions>>().Value.WorkingDirectory, "providers", $"{configurationId}.json");
}

/// <summary>
/// 供应商协议基类，默认通过模型数据源按供应商聚合模型。
/// Base class for vendor protocols that aggregates models from the shared data source by vendor.
/// </summary>
/// <typeparam name="TProtocolEnv">
/// 协议环境配置类型。
/// Protocol environment type.
/// </typeparam>
/// <param name="serviceProvider">
/// 用于解析日志记录器与共享模型目录服务的服务提供程序。
/// Service provider used to resolve the logger and shared model catalog services.
/// </param>
public abstract class VendorProtocolBase<TProtocolEnv>(IServiceProvider serviceProvider) : ProtocolBase<TProtocolEnv>(serviceProvider)
    where TProtocolEnv : ProtocolEnvBase,new()
{
    /// <inheritdoc />
    public override bool IsVendor => true;

    /// <summary>
    /// 该供应商协议对应的模型协议位标志。
    /// Model protocol bit flags exposed by the vendor protocol.
    /// </summary>
    public abstract ModelProtocolType ProtocolType { get; }

    /// <summary>
    /// 从共享 models.dev 目录加载当前供应商支持函数调用的文本模型。
    /// Loads the current vendor's text models with function-calling support from the shared models.dev catalog.
    /// </summary>
    /// <param name="cancellationToken">
    /// 取消模型发现流程的令牌。
    /// Token used to cancel model discovery.
    /// </param>
    /// <returns>
    /// 经过协议能力过滤后的模型信息集合。
    /// Model information entries filtered to the capabilities required by this protocol layer.
    /// </returns>
    public override async Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
    {
        // zh-CN: 默认实现直接复用 models.dev 的供应商目录，只返回同时支持文本输入、文本输出和函数调用的模型。
        // en: The default implementation reuses the models.dev vendor catalog and only returns models that support text input, text output, and function calling.
        var vendorId = ProtocolKey;
        var models = await ModelDataSource.GetModelsByVendorAsync(vendorId, cancellationToken);
        return
        [
            .. models
                .Where(x => x.InputModalities.HasFlag(ModelModality.Text)
                    && x.OutputModalities.HasFlag(ModelModality.Text)
                    && x.Capabilities.HasFlag(ModelCapability.FunctionCall))
                .Select(MapTo)
        ];
    }

    /// <summary>
    /// 将共享模型目录条目转换为协议层统一使用的 <see cref="ModelInfo" />。
    /// Converts a shared catalog entry into the <see cref="ModelInfo" /> shape used uniformly by protocols.
    /// </summary>
    /// <param name="source">
    /// models.dev 返回的原始模型数据。
    /// Raw model data returned by models.dev.
    /// </param>
    /// <returns>
    /// 仅保留协议发现所需标准标识的模型信息。
    /// Model information that keeps only the standardized identifiers needed for discovery.
    /// </returns>
    private ModelInfo MapTo(ModelData source)
    {
        return new ModelInfo(
            source.Id,
            source.VendorId,
            ProtocolType);
    }

}

/// <summary>
/// 协议环境配置基类。
/// Base class for protocol environment settings.
/// </summary>
public abstract class ProtocolEnvBase
{
    /// <summary>
    /// 协议调用的基础地址。
    /// Base URL used when calling the protocol endpoint.
    /// </summary>
    public abstract string? BaseUrl { get; set; }
}

/// <summary>
/// 带 API Key 的协议环境配置基类。
/// Base class for protocol environment settings that include an API key.
/// </summary>
public abstract class ProtocolApiKeyEnvironmentBase : ProtocolEnvBase
{
    /// <summary>
    /// 初始化带 API Key 的协议环境配置。
    /// Initializes protocol environment settings that include an API key.
    /// </summary>
    protected ProtocolApiKeyEnvironmentBase()
    {
        ApiKeyEnvName = ApiKeyFromEnvDefault;
    }

    /// <summary>
    /// 默认的 API Key 环境变量名称。
    /// Default environment variable name used to resolve the API key.
    /// </summary>
    protected abstract string ApiKeyFromEnvDefault { get; }

    /// <summary>
    /// 是否从环境变量读取 API Key。
    /// Indicates whether the API key should be read from an environment variable.
    /// </summary>
    public virtual bool ApiKeyFromEnv { get; set; }

    /// <summary>
    /// API Key 对应的环境变量名称。
    /// Environment variable name that stores the API key.
    /// </summary>
    public virtual string ApiKeyEnvName { get; set; }

    /// <summary>
    /// 直接存储的 API Key。
    /// API key stored directly in configuration.
    /// </summary>
    [Encrypted]
    public virtual string ApiKey { get; set; } = string.Empty;
}
public class DefaultProtocolApiKeyEnvironment : ProtocolApiKeyEnvironmentBase
{
    public override string? BaseUrl { get; set; }

    protected override string ApiKeyFromEnvDefault  {get; } = null!;
}
/// <summary>
/// 标记需要单独加密处理的协议配置属性。
/// Marks protocol configuration properties that should be encrypted independently.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EncryptedAttribute() : Attribute;
