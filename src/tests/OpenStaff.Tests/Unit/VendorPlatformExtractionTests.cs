using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent;
using OpenStaff.Configurations;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.Options;
using OpenStaff.Platform;
using OpenStaff.Plugin.Anthropic;
using OpenStaff.Plugin.Google;
using OpenStaff.Plugin.NewApi;
using OpenStaff.Plugin.OpenAI;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Tests.Unit;

public sealed class VendorPlatformExtractionTests : IDisposable
{
    private readonly string _rootWorkingDirectory = Path.Combine(
        AppContext.BaseDirectory,
        nameof(VendorPlatformExtractionTests),
        Guid.NewGuid().ToString("N"));

    public VendorPlatformExtractionTests()
    {
        Directory.CreateDirectory(_rootWorkingDirectory);
    }

    [Fact]
    public void NonCopilotPlatforms_ShouldKeepCapabilityOnlyBoundaries()
    {
        using var services = CreateServices(nameof(NonCopilotPlatforms_ShouldKeepCapabilityOnlyBoundaries));
        var anthropicPlatform = services.GetRequiredService<AnthropicPlatform>();
        Assert.IsAssignableFrom<IAgentProvider>(anthropicPlatform);
        Assert.IsNotAssignableFrom<IVendorPlatformMetadata>(anthropicPlatform);
        Assert.IsNotAssignableFrom<IVendorModelCatalogService>(anthropicPlatform);
        Assert.IsNotAssignableFrom<IVendorConfigurationService>(anthropicPlatform);

        IPlatform[] capabilityOnlyPlatforms =
        [
            services.GetRequiredService<GooglePlatform>(),
            services.GetRequiredService<OpenAIPlatform>(),
            services.GetRequiredService<NewApiPlatform>()
        ];

        foreach (var platform in capabilityOnlyPlatforms)
        {
            Assert.IsNotAssignableFrom<IAgentProvider>(platform);
            Assert.IsNotAssignableFrom<IVendorPlatformMetadata>(platform);
            Assert.IsNotAssignableFrom<IVendorModelCatalogService>(platform);
            Assert.IsNotAssignableFrom<IVendorConfigurationService>(platform);
        }
    }

    [Fact]
    public void NonCopilotPlatforms_ShouldDeclareExpectedCapabilities()
    {
        using var services = CreateServices(nameof(NonCopilotPlatforms_ShouldDeclareExpectedCapabilities));

        AssertPlatformCapabilities(
            services.GetRequiredService<AnthropicPlatform>(),
            typeof(AnthropicProtocol),
            "OpenStaff.Platform.AnthropicChatClientFactory",
            "OpenStaff.Plugin.Anthropic.AnthropicTaskAgentFactory",
            null,
            null,
            null);

        AssertPlatformCapabilities(
            services.GetRequiredService<GooglePlatform>(),
            typeof(GoogleProtocol),
            "OpenStaff.Platform.GoogleChatClientFactory",
            null,
            null,
            null,
            null);

        AssertPlatformCapabilities(
            services.GetRequiredService<OpenAIPlatform>(),
            typeof(OpenAIProtocol),
            "OpenStaff.Platform.OpenAIChatClientFactory",
            null,
            null,
            null,
            null);

        AssertPlatformCapabilities(
            services.GetRequiredService<NewApiPlatform>(),
            typeof(NewApiProtocol),
            "OpenStaff.Platform.NewApiChatClientFactory",
            null,
            typeof(NewApiPlatformMetadataService),
            null,
            null);
    }

    [Fact]
    public void VendorPlatformCatalog_ShouldOnlyIncludePlatformsThatExposeVendorMetadata()
    {
        using var services = CreateServices(
            nameof(VendorPlatformCatalog_ShouldOnlyIncludePlatformsThatExposeVendorMetadata),
            serviceCollection =>
            {
                serviceCollection.AddSingleton<AnthropicPlatformMetadataService>();
                serviceCollection.AddSingleton<AnthropicModelCatalogService>();
                serviceCollection.AddSingleton<AnthropicConfigurationService>();
                serviceCollection.AddSingleton<GooglePlatformMetadataService>();
                serviceCollection.AddSingleton<GoogleModelCatalogService>();
                serviceCollection.AddSingleton<GoogleConfigurationService>();
                serviceCollection.AddSingleton<OpenAIPlatformMetadataService>();
                serviceCollection.AddSingleton<OpenAIModelCatalogService>();
                serviceCollection.AddSingleton<NewApiPlatformMetadataService>();
            });

        var catalog = new VendorPlatformCatalog(
            new PlatformRegistry(
            [
                services.GetRequiredService<AnthropicPlatform>(),
                services.GetRequiredService<GooglePlatform>(),
                services.GetRequiredService<OpenAIPlatform>(),
                services.GetRequiredService<NewApiPlatform>()
            ]));

        Assert.False(catalog.TryGetVendorPlatform("anthropic", out _));
        Assert.False(catalog.TryGetVendorPlatform("google", out _));
        Assert.False(catalog.TryGetVendorPlatform("openai", out _));

        Assert.True(catalog.TryGetVendorPlatform("newapi", out var newApiPlatform));
        Assert.IsType<NewApiPlatformMetadataService>(newApiPlatform.Metadata);
        Assert.Null(newApiPlatform.ModelCatalog);
        Assert.Null(newApiPlatform.Configuration);
    }

    [Fact]
    public async Task GooglePlatform_AgentFactory_ShouldRejectWhenNoTaskAgentCapabilityIsRegistered()
    {
        using var services = CreateServices(
            nameof(GooglePlatform_AgentFactory_ShouldRejectWhenNoTaskAgentCapabilityIsRegistered));
        var googlePlatform = services.GetRequiredService<GooglePlatform>();

        var factory = new AgentFactory(new PlatformRegistry([googlePlatform]), [], services);
        var role = new AgentRole
        {
            Name = "Gemini",
            ProviderType = "google",
            ModelName = "gemini-2.5-flash"
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            factory.CreateAgentAsync(
                role,
                new AgentContext { Role = role }));

        Assert.Contains("Agent provider 'google' is not registered", exception.Message);
    }

    [Fact]
    public async Task GoogleVendorServices_ShouldExposeConfigurationMetadata_AndDedicatedCatalog()
    {
        using var services = CreateServices(
            nameof(GoogleVendorServices_ShouldExposeConfigurationMetadata_AndDedicatedCatalog),
            serviceCollection =>
            {
                serviceCollection.AddSingleton<IModelDataSource>(new StubModelDataSource(
                    isReady: true,
                    modelsByVendor: new Dictionary<string, IReadOnlyList<ModelData>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["google"] =
                        [
                            CreateModel("gemini-2.5-flash", "Gemini 2.5 Flash", "google", "Gemini 2.5")
                        ]
                    }));
                serviceCollection.AddSingleton<GooglePlatformMetadataService>();
                serviceCollection.AddSingleton<GoogleConfigurationService>();
                serviceCollection.AddSingleton<GoogleModelCatalogService>();
            });

        var metadataService = services.GetRequiredService<GooglePlatformMetadataService>();
        var configurationService = services.GetRequiredService<GoogleConfigurationService>();
        var modelCatalogService = services.GetRequiredService<GoogleModelCatalogService>();

        Assert.Equal("google", metadataService.ProviderType);
        Assert.Equal("Google Gemini", metadataService.DisplayName);
        Assert.NotSame(metadataService, configurationService);
        Assert.NotSame(metadataService, modelCatalogService);
        Assert.NotSame(configurationService, modelCatalogService);

        var configuration = await configurationService.GetConfigurationAsync();
        Assert.False(configuration.Configuration.UseVertexAI);
        Assert.Null(configuration.Configuration.ApiKey);
        Assert.Null(configuration.Configuration.BaseUrl);
        Assert.Collection(configuration.Properties,
            property =>
            {
                Assert.Equal(nameof(GooglePlatformConfiguration.UseVertexAI), property.Name);
                Assert.Equal(ConfigurationPropertyType.Boolean, property.Type);
                Assert.Equal(false, property.DefaultValue);
                Assert.True(property.Required);
            },
            property =>
            {
                Assert.Equal(nameof(GooglePlatformConfiguration.ApiKey), property.Name);
                Assert.Equal(ConfigurationPropertyType.String, property.Type);
                Assert.Null(property.DefaultValue);
                Assert.True(property.Required);
            },
            property =>
            {
                Assert.Equal(nameof(GooglePlatformConfiguration.BaseUrl), property.Name);
                Assert.Equal(ConfigurationPropertyType.String, property.Type);
                Assert.Equal("https://generativelanguage.googleapis.com", property.DefaultValue);
                Assert.False(property.Required);
            });

        var catalog = await modelCatalogService.GetModelCatalogAsync();
        Assert.Equal(VendorModelCatalogStatus.Ready, catalog.Status);
        var model = Assert.Single(catalog.Models);
        Assert.Equal("gemini-2.5-flash", model.Id);
        Assert.Equal("Gemini 2.5 Flash", model.Name);
        Assert.Equal("Gemini 2.5", model.Family);
    }

    [Fact]
    public async Task OpenAIVendorServices_ShouldProvideDedicatedMetadataAndFallbackCatalog_WithoutLocalConfigurationService()
    {
        using var services = CreateServices(
            nameof(OpenAIVendorServices_ShouldProvideDedicatedMetadataAndFallbackCatalog_WithoutLocalConfigurationService),
            serviceCollection =>
            {
                serviceCollection.AddSingleton<IModelDataSource>(new StubModelDataSource(isReady: false));
                serviceCollection.AddSingleton<OpenAIPlatformMetadataService>();
                serviceCollection.AddSingleton<OpenAIModelCatalogService>();
            });

        var metadataService = services.GetRequiredService<OpenAIPlatformMetadataService>();
        var modelCatalogService = services.GetRequiredService<OpenAIModelCatalogService>();

        Assert.Equal("openai", metadataService.ProviderType);
        Assert.Equal("OpenAI", metadataService.DisplayName);
        Assert.IsNotAssignableFrom<IVendorConfigurationService>(modelCatalogService);
        Assert.NotSame(metadataService, modelCatalogService);

        var catalog = await modelCatalogService.GetModelCatalogAsync();
        Assert.Equal(VendorModelCatalogStatus.Ready, catalog.Status);
        Assert.Contains(catalog.Models, model => model.Id == "gpt-4o");
        Assert.Contains(catalog.Models, model => model.Id == "gpt-4.1");
        Assert.Contains(catalog.Models, model => model.Id == "o3-mini");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootWorkingDirectory))
            Directory.Delete(_rootWorkingDirectory, recursive: true);
    }

    private ServiceProvider CreateServices(string scopeName, Action<IServiceCollection>? configureServices = null)
    {
        var workingDirectory = Path.Combine(_rootWorkingDirectory, scopeName);
        Directory.CreateDirectory(workingDirectory);

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHttpClientFactory>(new StubHttpClientFactory())
            .AddSingleton<IModelDataSource>(new StubModelDataSource())
            .AddSingleton<AnthropicPlatformMetadataService>()
            .AddSingleton<AnthropicModelCatalogService>()
            .AddSingleton<AnthropicConfigurationService>()
            .AddSingleton<AnthropicTaskAgentFactory>()
            .AddSingleton<AnthropicPlatform>()
            .AddSingleton<GooglePlatformMetadataService>()
            .AddSingleton<GoogleModelCatalogService>()
            .AddSingleton<GoogleConfigurationService>()
            .AddSingleton<GoogleTaskAgentFactory>()
            .AddSingleton<GooglePlatform>()
            .AddSingleton<OpenAIPlatformMetadataService>()
            .AddSingleton<OpenAIModelCatalogService>()
            .AddSingleton<OpenAITaskAgentFactory>()
            .AddSingleton<OpenAIPlatform>()
            .AddSingleton<NewApiPlatformMetadataService>()
            .AddSingleton<NewApiPlatform>()
            .Configure<OpenStaffOptions>(options => options.WorkingDirectory = workingDirectory);

        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static void AssertPlatformCapabilities(
        IPlatform platform,
        Type expectedProtocolType,
        string expectedChatClientFactoryTypeName,
        string? expectedTaskAgentFactoryTypeName,
        Type? expectedMetadataServiceType,
        Type? expectedModelCatalogServiceType,
        Type? expectedConfigurationServiceType)
    {
        var protocol = Assert.IsAssignableFrom<IHasProtocol>(platform).GetProtocol();
        Assert.IsAssignableFrom<IProtocol>(protocol);
        Assert.Equal(expectedProtocolType, protocol.GetType());
        Assert.Equal(expectedChatClientFactoryTypeName, Assert.IsAssignableFrom<IHasChatClientFactory>(platform).GetChatClientFactory().GetType().FullName);
        Assert.Equal(expectedTaskAgentFactoryTypeName, (platform as IHasTaskAgentFactory)?.GetTaskAgentFactory().FactoryType.FullName);

        if (expectedMetadataServiceType is null)
            Assert.IsNotAssignableFrom<IHasVendorMetadata>(platform);
        else
            Assert.Equal(expectedMetadataServiceType, Assert.IsAssignableFrom<IHasVendorMetadata>(platform).GetVendorMetadataService().GetType());

        if (expectedModelCatalogServiceType is null)
            Assert.IsNotAssignableFrom<IHasModelCatalog>(platform);
        else
            Assert.Equal(expectedModelCatalogServiceType, Assert.IsAssignableFrom<IHasModelCatalog>(platform).GetModelCatalogService().GetType());

        if (expectedConfigurationServiceType is null)
            Assert.IsNotAssignableFrom<IHasConfiguration>(platform);
        else
            Assert.Equal(expectedConfigurationServiceType, Assert.IsAssignableFrom<IHasConfiguration>(platform).GetConfigurationService().GetType());
    }

    private static ModelData CreateModel(string id, string name, string vendorId, string? family = null)
        => new(
            id,
            name,
            vendorId,
            family,
            ReleasedAt: null,
            InputModalities: ModelModality.Text,
            OutputModalities: ModelModality.Text,
            Capabilities: ModelCapability.FunctionCall,
            Limits: new ModelLimits(null, null, null),
            Pricing: new ModelPricing(null, null, null, null));

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class StubModelDataSource(
        bool isReady = false,
        IReadOnlyDictionary<string, IReadOnlyList<ModelData>>? modelsByVendor = null) : IModelDataSource
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<ModelData>> _modelsByVendor =
            modelsByVendor ?? new Dictionary<string, IReadOnlyList<ModelData>>(StringComparer.OrdinalIgnoreCase);

        public string SourceId => "stub";
        public string DisplayName => "Stub model data source";
        public bool IsReady => isReady;
        public DateTime? LastUpdatedUtc => null;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ModelVendor>> GetVendorsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModelVendor>>(
                _modelsByVendor.Keys
                    .Select(vendorId => new ModelVendor(vendorId, vendorId, null, null, []))
                    .ToList());

        public Task<IReadOnlyList<ModelData>> GetModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModelData>>(
                _modelsByVendor.Values
                    .SelectMany(models => models)
                    .ToList());

        public Task<IReadOnlyList<ModelData>> GetModelsByVendorAsync(string vendorId, CancellationToken cancellationToken = default)
            => Task.FromResult(
                _modelsByVendor.TryGetValue(vendorId, out var models)
                    ? models
                    : (IReadOnlyList<ModelData>)[]);

        public Task<ModelData?> GetModelAsync(string vendorId, string modelId, CancellationToken cancellationToken = default)
            => Task.FromResult(
                _modelsByVendor.TryGetValue(vendorId, out var models)
                    ? models.FirstOrDefault(model => string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase))
                    : null);
    }
}
