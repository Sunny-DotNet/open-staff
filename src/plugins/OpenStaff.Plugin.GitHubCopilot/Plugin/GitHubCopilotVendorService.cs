using Microsoft.Extensions.Options;
using OpenStaff.Agent;
using OpenStaff.Configurations;
using OpenStaff.Options;

namespace OpenStaff.Agent.Vendor.GitHubCopilot;

public class GitHubCopilotPlatformConfiguration
{
    public bool? Streaming { get; set; }
    public bool? AutoApproved { get; set; }
}

public sealed class GitHubCopilotPlatformMetadataService : VendorPlatformMetadataBase
{
    public override string ProviderType => "github-copilot";

    public override string DisplayName => "GitHub Copilot";

    public override string? AvatarDataUri => "https://unpkg.com/@lobehub/icons-static-png@latest/light/githubcopilot.png";
}

public sealed class GitHubCopilotModelCatalogService(IGitHubCopilotClientHost clientHost) : VendorModelCatalogServiceBase
{
    protected override async Task<IReadOnlyList<VendorModel>> GetModelsAsync(CancellationToken ct = default)
    {
        var client = await clientHost.GetClientAsync(ct);
        var models = await client.ListModelsAsync(ct);
        return models.Select(m => new VendorModel(m.Id, m.Name)).ToList();
    }
}

public sealed class GitHubCopilotConfigurationService(
    IOptions<OpenStaffOptions> openStaffOptions) : VendorConfigurationServiceBase<GitHubCopilotPlatformConfiguration>(openStaffOptions)
{
    public override string ProviderType => "github-copilot";

    public override ConfigurationProperty[] ConfigurationProperties { get; } =
    [
        new(nameof(GitHubCopilotPlatformConfiguration.Streaming), ConfigurationPropertyType.Boolean, true, false),
        new(nameof(GitHubCopilotPlatformConfiguration.AutoApproved), ConfigurationPropertyType.Boolean, false, false)
    ];

    internal async Task<GitHubCopilotPlatformConfiguration> GetEffectiveConfigurationAsync(CancellationToken ct = default)
    {
        var configuration = (await GetConfigurationAsync(ct)).Configuration;
        configuration.Streaming ??= true;
        configuration.AutoApproved ??= false;
        return configuration;
    }
}
