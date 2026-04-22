using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent;
using OpenStaff.Plugins.ModelDataSource;

namespace OpenStaff.Plugin.OpenAI;

public sealed class OpenAIPlatformMetadataService : VendorPlatformMetadataBase
{
    public override string ProviderType => "openai";

    public override string DisplayName => "OpenAI";

    public override string? AvatarDataUri => "https://unpkg.com/@lobehub/icons-static-png@latest/light/openai.png";
}

public sealed class OpenAIModelCatalogService(IServiceProvider serviceProvider) : VendorModelCatalogServiceBase
{
    private const string VendorId = "openai";

    private static readonly VendorModel[] FallbackModels =
    [
        new("gpt-4o", "GPT-4o", "GPT-4o"),
        new("gpt-4o-mini", "GPT-4o Mini", "GPT-4o"),
        new("gpt-4.1", "GPT-4.1", "GPT-4.1"),
        new("gpt-4.1-mini", "GPT-4.1 Mini", "GPT-4.1"),
        new("o3-mini", "o3-mini", "o3")
    ];

    protected override async Task<IReadOnlyList<VendorModel>> GetModelsAsync(CancellationToken ct = default)
    {
        var modelDataSource = serviceProvider.GetService<IModelDataSource>();
        if (modelDataSource is { IsReady: true })
        {
            var models = await modelDataSource.GetModelsByVendorAsync(VendorId, ct);
            if (models.Count > 0)
                return models.Select(m => new VendorModel(m.Id, m.Name, m.Family)).ToList();
        }

        return FallbackModels;
    }
}
