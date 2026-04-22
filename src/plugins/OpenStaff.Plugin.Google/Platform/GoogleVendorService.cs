using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenStaff.Agent;
using OpenStaff.Configurations;
using OpenStaff.Options;
using OpenStaff.Plugins.ModelDataSource;

namespace OpenStaff.Plugin.Google;

public class GooglePlatformConfiguration
{
    public bool UseVertexAI { get; set; }
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
}

public sealed class GooglePlatformMetadataService : VendorPlatformMetadataBase
{
    public override string ProviderType => "google";

    public override string DisplayName => "Google Gemini";

    public override string? AvatarDataUri => "https://unpkg.com/@lobehub/icons-static-png@latest/light/gemini.png";
}

public sealed class GoogleConfigurationService(
    IOptions<OpenStaffOptions> openStaffOptions) : VendorConfigurationServiceBase<GooglePlatformConfiguration>(openStaffOptions)
{
    private const string DefaultBaseUrl = "https://generativelanguage.googleapis.com";

    public override string ProviderType => "google";

    public override ConfigurationProperty[] ConfigurationProperties =>
    [
        new(nameof(GooglePlatformConfiguration.UseVertexAI), ConfigurationPropertyType.Boolean, false, true),
        new(nameof(GooglePlatformConfiguration.ApiKey), ConfigurationPropertyType.String, null, true),
        new(nameof(GooglePlatformConfiguration.BaseUrl), ConfigurationPropertyType.String, DefaultBaseUrl, false)
    ];

    internal async Task<GooglePlatformConfiguration> GetEffectiveConfigurationAsync(CancellationToken ct = default)
    {
        var configuration = (await GetConfigurationAsync(ct)).Configuration;
        if (string.IsNullOrWhiteSpace(configuration.ApiKey))
            throw new InvalidOperationException("请先在供应商管理中配置 Google API Key");

        return configuration;
    }
}

public sealed class GoogleModelCatalogService(IServiceProvider serviceProvider) : VendorModelCatalogServiceBase
{
    private const string VendorId = "google";

    protected override async Task<IReadOnlyList<VendorModel>> GetModelsAsync(CancellationToken ct = default)
    {
        var modelDataSource = serviceProvider.GetService<IModelDataSource>();
        if (modelDataSource is { IsReady: true })
        {
            var models = await modelDataSource.GetModelsByVendorAsync(VendorId, ct);
            if (models.Count > 0)
                return models.Select(m => new VendorModel(m.Id, m.Name, m.Family)).ToList();
        }

        return [];
    }
}
