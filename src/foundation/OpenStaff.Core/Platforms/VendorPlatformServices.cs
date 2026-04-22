using Microsoft.Extensions.Options;
using OpenStaff.Configurations;
using OpenStaff.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenStaff.Agent;

/// <summary>
/// zh-CN: 描述 Vendor 平台的展示元数据。
/// en: Describes the display metadata exposed by a vendor platform.
/// </summary>
public interface IVendorPlatformMetadata
{
    string ProviderType { get; }
    string DisplayName { get; }
    string? AvatarDataUri { get; }
}

/// <summary>
/// zh-CN: 定义 Vendor 模型目录查询契约。
/// en: Defines the contract for vendor model-catalog discovery.
/// </summary>
public interface IVendorModelCatalogService
{
    Task<VendorModelCatalogResult> GetModelCatalogAsync(CancellationToken ct = default);
}

/// <summary>
/// zh-CN: 定义 Vendor 级本地配置读写契约。
/// en: Defines the contract for vendor-level local configuration persistence.
/// </summary>
public interface IVendorConfigurationService
{
    ConfigurationProperty[] ConfigurationProperties { get; }
    Task<Dictionary<string, object?>> GetConfigurationValuesAsync(CancellationToken ct = default);
    Task SetConfigurationValuesAsync(Dictionary<string, object?> configuration, CancellationToken ct = default);
}

public interface IVendorConfigurationService<TConfiguration> : IVendorConfigurationService
    where TConfiguration : new()
{
    Task<GetConfigurationResult<TConfiguration>> GetConfigurationAsync(CancellationToken ct = default);
    Task SetConfigurationAsync(TConfiguration configuration, CancellationToken ct = default);
}

public abstract class VendorPlatformMetadataBase : IVendorPlatformMetadata
{
    public abstract string ProviderType { get; }
    public abstract string DisplayName { get; }
    public virtual string? AvatarDataUri => null;
}

public abstract class VendorModelCatalogServiceBase : IVendorModelCatalogService
{
    protected abstract Task<IReadOnlyList<VendorModel>> GetModelsAsync(CancellationToken ct = default);

    public virtual async Task<VendorModelCatalogResult> GetModelCatalogAsync(CancellationToken ct = default)
        => VendorModelCatalogResult.Ready(await GetModelsAsync(ct));
}

public abstract class VendorConfigurationServiceBase<TConfiguration> : IVendorConfigurationService<TConfiguration>
    where TConfiguration : new()
{
    private static readonly JsonSerializerOptions RuntimeSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public abstract string ProviderType { get; }
    public abstract ConfigurationProperty[] ConfigurationProperties { get; }
    protected OpenStaffOptions OpenStaffOptions { get; }
    protected Lazy<string> LazyConfigurationFilename { get; }

    protected VendorConfigurationServiceBase(IOptions<OpenStaffOptions> openStaffOptions)
    {
        OpenStaffOptions = openStaffOptions.Value;
        LazyConfigurationFilename = new Lazy<string>(InitializeConfigurationFilename);
    }

    private string InitializeConfigurationFilename()
    {
        var configurationFilename = Path.Combine(OpenStaffOptions.WorkingDirectory, "agents", ProviderType, "configuration.json");
        var directory = Path.GetDirectoryName(configurationFilename);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory!);
        return configurationFilename;
    }

    public virtual async Task<GetConfigurationResult<TConfiguration>> GetConfigurationAsync(CancellationToken ct = default)
    {
        if (File.Exists(LazyConfigurationFilename.Value))
        {
            using var stream = File.OpenRead(LazyConfigurationFilename.Value);
            var configuration = await JsonSerializer.DeserializeAsync<TConfiguration>(stream, cancellationToken: ct);
            return new GetConfigurationResult<TConfiguration>(ConfigurationProperties, configuration ?? new());
        }

        return new GetConfigurationResult<TConfiguration>(ConfigurationProperties, new());
    }

    public async Task SetConfigurationAsync(TConfiguration configuration, CancellationToken ct = default)
    {
        using var stream = File.Create(LazyConfigurationFilename.Value);
        await JsonSerializer.SerializeAsync(stream, configuration, RuntimeSerializerOptions, ct);
    }

    public virtual async Task<Dictionary<string, object?>> GetConfigurationValuesAsync(CancellationToken ct = default)
    {
        var configuration = (await GetConfigurationAsync(ct)).Configuration;
        var values = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                         JsonSerializer.Serialize(configuration, RuntimeSerializerOptions),
                         RuntimeSerializerOptions)
                     ?? [];
        return NormalizeConfigurationValues(values);
    }

    public virtual async Task SetConfigurationValuesAsync(Dictionary<string, object?> configuration, CancellationToken ct = default)
    {
        var typedConfiguration = JsonSerializer.Deserialize<TConfiguration>(
                                     JsonSerializer.Serialize(configuration, RuntimeSerializerOptions),
                                     RuntimeSerializerOptions)
                                 ?? new();
        await SetConfigurationAsync(typedConfiguration, ct);
    }

    private static Dictionary<string, object?> NormalizeConfigurationValues(Dictionary<string, object?> values)
        => values.ToDictionary(pair => pair.Key, pair => NormalizeConfigurationValue(pair.Value));

    private static object? NormalizeConfigurationValue(object? value)
        => value switch
        {
            JsonElement element => element.ValueKind switch
            {
                JsonValueKind.False => false,
                JsonValueKind.True => true,
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.Number when element.TryGetInt64(out var int64Value) => int64Value,
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.String => element.GetString(),
                _ => element.ToString()
            },
            _ => value
        };
}

/// <summary>
/// zh-CN: 描述一个可由厂商提供程序返回的模型条目。
/// en: Describes a model entry returned by a vendor provider.
/// </summary>
/// <param name="Id">
/// zh-CN: 模型的稳定标识。
/// en: The stable model identifier.
/// </param>
/// <param name="Name">
/// zh-CN: 用于展示的模型名称。
/// en: The display name shown to users.
/// </param>
/// <param name="Family">
/// zh-CN: 模型家族名称，例如 Claude 4。
/// en: The model family, such as Claude 4.
/// </param>
/// <param name="Description">
/// zh-CN: 可选的补充说明。
/// en: Optional descriptive text.
/// </param>
public record VendorModel(
    string Id,
    string Name,
    string? Family = null,
    string? Description = null);

public enum VendorModelCatalogStatus
{
    Ready,
    RequiresProviderConfiguration,
    LoadFailed
}

public sealed record VendorModelCatalogResult(
    VendorModelCatalogStatus Status,
    IReadOnlyList<VendorModel> Models,
    string? Message = null,
    IReadOnlyList<string>? MissingConfigurationFields = null)
{
    public static VendorModelCatalogResult Ready(IReadOnlyList<VendorModel> models)
        => new(VendorModelCatalogStatus.Ready, models);

    public static VendorModelCatalogResult RequiresProviderConfiguration(string message, params string[] missingConfigurationFields)
        => new(VendorModelCatalogStatus.RequiresProviderConfiguration, [], message, missingConfigurationFields);

    public static VendorModelCatalogResult LoadFailed(string message, IReadOnlyList<VendorModel>? models = null)
        => new(VendorModelCatalogStatus.LoadFailed, models ?? [], message);
}
