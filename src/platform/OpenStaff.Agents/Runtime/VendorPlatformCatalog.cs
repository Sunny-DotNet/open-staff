using OpenStaff.Provider.Platforms;

namespace OpenStaff.Agent;

public sealed record VendorPlatformServices(
    IVendorPlatformMetadata Metadata,
    IVendorModelCatalogService? ModelCatalog = null,
    IVendorConfigurationService? Configuration = null);

public interface IVendorPlatformCatalog
{
    IReadOnlyDictionary<string, VendorPlatformServices> Platforms { get; }

    bool TryGetVendorPlatform(string providerType, out VendorPlatformServices platform);
}

public sealed class VendorPlatformCatalog(IPlatformRegistry platformRegistry) : IVendorPlatformCatalog
{
    private readonly Lazy<IReadOnlyDictionary<string, VendorPlatformServices>> _platforms = new(() =>
    {
        var resolved = new Dictionary<string, VendorPlatformServices>(StringComparer.OrdinalIgnoreCase);

        foreach (var platform in platformRegistry.Platforms.Values)
        {
            var modelCatalogPlatform = platform as IHasModelCatalog;
            var configurationPlatform = platform as IHasConfiguration;

            if (platform is not IHasVendorMetadata vendorMetadataPlatform)
            {
                if (modelCatalogPlatform is not null || configurationPlatform is not null)
                {
                    throw new InvalidOperationException(
                        $"Platform '{platform.PlatformKey}' declares vendor model catalog or configuration without vendor metadata.");
                }

                continue;
            }

            var metadata = vendorMetadataPlatform.GetVendorMetadataService()
                           ?? throw new InvalidOperationException($"Platform '{platform.PlatformKey}' returned a null vendor metadata service.");
            var modelCatalog = modelCatalogPlatform?.GetModelCatalogService();
            var configuration = configurationPlatform?.GetConfigurationService();

            ValidateDistinctServiceTypes(
                platform.PlatformKey,
                metadata,
                modelCatalog,
                configuration);

            if (!string.Equals(metadata.ProviderType, platform.PlatformKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Vendor metadata service '{metadata.GetType().FullName}' resolved provider type '{metadata.ProviderType}', expected '{platform.PlatformKey}'.");
            }

            resolved[platform.PlatformKey] = new VendorPlatformServices(metadata, modelCatalog, configuration);
        }

        return resolved;
    });

    public IReadOnlyDictionary<string, VendorPlatformServices> Platforms => _platforms.Value;

    public bool TryGetVendorPlatform(string providerType, out VendorPlatformServices platform)
        => Platforms.TryGetValue(providerType, out platform!);

    private static void ValidateDistinctServiceTypes(
        string platformKey,
        IVendorPlatformMetadata metadata,
        IVendorModelCatalogService? modelCatalog,
        IVendorConfigurationService? configuration)
    {
        var metadataServiceType = metadata.GetType();
        var modelCatalogServiceType = modelCatalog?.GetType();
        var configurationServiceType = configuration?.GetType();

        if (modelCatalogServiceType == metadataServiceType)
        {
            throw new InvalidOperationException(
                $"Platform '{platformKey}' must use distinct service types for vendor metadata and model catalog.");
        }

        if (configurationServiceType == metadataServiceType)
        {
            throw new InvalidOperationException(
                $"Platform '{platformKey}' must use distinct service types for vendor metadata and configuration.");
        }

        if (modelCatalogServiceType is not null && configurationServiceType == modelCatalogServiceType)
        {
            throw new InvalidOperationException(
                $"Platform '{platformKey}' must use distinct service types for vendor model catalog and configuration.");
        }
    }
}
