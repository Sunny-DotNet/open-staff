using GitHub.Copilot.SDK;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Agent;
using OpenStaff.Agent.Services;
using OpenStaff.Agent.Vendor.GitHubCopilot;
using OpenStaff.Configurations;
using OpenStaff.Core.Agents;
using OpenStaff.Entities;
using OpenStaff.Options;
using OpenStaff.Plugin.Anthropic;
using OpenStaff.Plugin.Google;
using OpenStaff.Plugin.NewApi;
using OpenStaff.Plugin.OpenAI;
using OpenStaff.Plugin.Services;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider.Protocols;
using System.Net;
using System.Net.Http;
using System.Text;
using OpenStaff.Plugin;
using OpenStaff.Platform;
using OpenStaff.Provider.Platforms;

namespace OpenStaff.Tests.Unit;

public sealed class VendorPlatformTests : IDisposable
{
    private readonly string _rootWorkingDirectory = Path.Combine(
        AppContext.BaseDirectory,
        nameof(VendorPlatformTests),
        Guid.NewGuid().ToString("N"));

    public VendorPlatformTests()
    {
        Directory.CreateDirectory(_rootWorkingDirectory);
    }

    [Fact]
    public void Platforms_ShouldExposeCapabilitiesWithoutImplementingVendorServices()
    {
        using var services = CreateServices(nameof(Platforms_ShouldExposeCapabilitiesWithoutImplementingVendorServices), serviceCollection =>
        {
            serviceCollection.AddSingleton<GooglePlatformMetadataService>();
            serviceCollection.AddSingleton<GoogleModelCatalogService>();
            serviceCollection.AddSingleton<GoogleConfigurationService>();
            serviceCollection.AddSingleton<GoogleTaskAgentFactory>();
            serviceCollection.AddSingleton<GooglePlatform>();
            serviceCollection.AddSingleton<OpenAIPlatformMetadataService>();
            serviceCollection.AddSingleton<OpenAIModelCatalogService>();
            serviceCollection.AddSingleton<OpenAITaskAgentFactory>();
            serviceCollection.AddSingleton<OpenAIPlatform>();
            serviceCollection.AddSingleton<NewApiPlatformMetadataService>();
            serviceCollection.AddSingleton<NewApiPlatform>();
        });

        var anthropicPlatform = services.GetRequiredService<AnthropicPlatform>();
        Assert.IsAssignableFrom<IHasProtocol>(anthropicPlatform);
        Assert.IsAssignableFrom<IHasChatClientFactory>(anthropicPlatform);
        Assert.IsAssignableFrom<IHasTaskAgentFactory>(anthropicPlatform);
        Assert.IsNotAssignableFrom<IHasVendorMetadata>(anthropicPlatform);
        Assert.IsNotAssignableFrom<IHasModelCatalog>(anthropicPlatform);
        Assert.IsNotAssignableFrom<IHasConfiguration>(anthropicPlatform);
        Assert.IsAssignableFrom<IAgentProvider>(anthropicPlatform);
        Assert.IsNotAssignableFrom<ITaskAgentFactory>(anthropicPlatform);
        Assert.IsNotAssignableFrom<IVendorPlatformMetadata>(anthropicPlatform);
        Assert.IsNotAssignableFrom<IVendorModelCatalogService>(anthropicPlatform);
        Assert.IsNotAssignableFrom<IVendorConfigurationService>(anthropicPlatform);
        AssertDirectProtocol<AnthropicProtocol>(anthropicPlatform);

        var gitHubCopilotPlatform = services.GetRequiredService<GitHubCopilotPlatform>();
        Assert.IsAssignableFrom<IHasProtocol>(gitHubCopilotPlatform);
        Assert.IsNotAssignableFrom<IHasChatClientFactory>(gitHubCopilotPlatform);
        Assert.IsAssignableFrom<IHasTaskAgentFactory>(gitHubCopilotPlatform);
        Assert.IsNotAssignableFrom<IHasVendorMetadata>(gitHubCopilotPlatform);
        Assert.IsNotAssignableFrom<IHasModelCatalog>(gitHubCopilotPlatform);
        Assert.IsNotAssignableFrom<IHasConfiguration>(gitHubCopilotPlatform);
        Assert.IsAssignableFrom<IAgentProvider>(gitHubCopilotPlatform);
        Assert.IsNotAssignableFrom<ITaskAgentFactory>(gitHubCopilotPlatform);
        Assert.IsNotAssignableFrom<IVendorPlatformMetadata>(gitHubCopilotPlatform);
        Assert.IsNotAssignableFrom<IVendorModelCatalogService>(gitHubCopilotPlatform);
        Assert.IsNotAssignableFrom<IVendorConfigurationService>(gitHubCopilotPlatform);
        AssertDirectProtocol<GitHubCopilotProtocol>(gitHubCopilotPlatform);

        var googlePlatform = services.GetRequiredService<GooglePlatform>();
        Assert.IsAssignableFrom<IHasProtocol>(googlePlatform);
        Assert.IsAssignableFrom<IHasChatClientFactory>(googlePlatform);
        Assert.IsNotAssignableFrom<IHasTaskAgentFactory>(googlePlatform);
        Assert.IsNotAssignableFrom<IHasVendorMetadata>(googlePlatform);
        Assert.IsNotAssignableFrom<IHasModelCatalog>(googlePlatform);
        Assert.IsNotAssignableFrom<IHasConfiguration>(googlePlatform);
        Assert.IsNotAssignableFrom<IAgentProvider>(googlePlatform);
        Assert.IsNotAssignableFrom<IVendorPlatformMetadata>(googlePlatform);
        Assert.IsNotAssignableFrom<IVendorModelCatalogService>(googlePlatform);
        Assert.IsNotAssignableFrom<IVendorConfigurationService>(googlePlatform);
        AssertDirectProtocol<GoogleProtocol>(googlePlatform);

        var openAiPlatform = services.GetRequiredService<OpenAIPlatform>();
        Assert.IsAssignableFrom<IHasProtocol>(openAiPlatform);
        Assert.IsAssignableFrom<IHasChatClientFactory>(openAiPlatform);
        Assert.IsNotAssignableFrom<IHasTaskAgentFactory>(openAiPlatform);
        Assert.IsNotAssignableFrom<IHasVendorMetadata>(openAiPlatform);
        Assert.IsNotAssignableFrom<IHasModelCatalog>(openAiPlatform);
        Assert.IsNotAssignableFrom<IHasConfiguration>(openAiPlatform);
        Assert.IsNotAssignableFrom<IAgentProvider>(openAiPlatform);
        Assert.IsNotAssignableFrom<IVendorPlatformMetadata>(openAiPlatform);
        Assert.IsNotAssignableFrom<IVendorModelCatalogService>(openAiPlatform);
        Assert.IsNotAssignableFrom<IVendorConfigurationService>(openAiPlatform);
        AssertDirectProtocol<OpenAIProtocol>(openAiPlatform);

        var newApiPlatform = services.GetRequiredService<NewApiPlatform>();
        Assert.IsAssignableFrom<IHasProtocol>(newApiPlatform);
        Assert.IsAssignableFrom<IHasChatClientFactory>(newApiPlatform);
        Assert.IsAssignableFrom<IHasVendorMetadata>(newApiPlatform);
        Assert.IsNotAssignableFrom<IHasModelCatalog>(newApiPlatform);
        Assert.IsNotAssignableFrom<IHasConfiguration>(newApiPlatform);
        Assert.IsNotAssignableFrom<IHasTaskAgentFactory>(newApiPlatform);
        Assert.IsNotAssignableFrom<IAgentProvider>(newApiPlatform);
        Assert.IsNotAssignableFrom<IVendorPlatformMetadata>(newApiPlatform);
        Assert.IsNotAssignableFrom<IVendorModelCatalogService>(newApiPlatform);
        Assert.IsNotAssignableFrom<IVendorConfigurationService>(newApiPlatform);
        AssertDirectProtocol<NewApiProtocol>(newApiPlatform);
    }

    [Fact]
    public void VendorPlatformCatalog_ShouldRejectCollapsedServiceTypes()
    {
        var catalog = new VendorPlatformCatalog(
            new PlatformRegistry([new CollapsedVendorPlatform()]));

        var exception = Assert.Throws<InvalidOperationException>(() => _ = catalog.Platforms);
        Assert.Contains("distinct service types", exception.Message);
    }

    [Fact]
    public void VendorServiceBases_ShouldKeepResponsibilitiesIndependent()
    {
        Assert.Equal(typeof(object), typeof(VendorModelCatalogServiceBase).BaseType);
        Assert.Equal(typeof(object), typeof(VendorConfigurationServiceBase<>).BaseType);
        Assert.False(typeof(IVendorPlatformMetadata).IsAssignableFrom(typeof(GitHubCopilotModelCatalogService)));
        Assert.False(typeof(IVendorPlatformMetadata).IsAssignableFrom(typeof(AnthropicConfigurationService)));
        Assert.False(typeof(IVendorModelCatalogService).IsAssignableFrom(typeof(AnthropicConfigurationService)));
        Assert.False(typeof(IVendorConfigurationService).IsAssignableFrom(typeof(OpenAIModelCatalogService)));
    }

    [Fact]
    public void GitHubCopilotPlatform_ShouldExposeProtocolAndTaskAgentCapabilities()
    {
        using var services = CreateServices(nameof(GitHubCopilotPlatform_ShouldExposeProtocolAndTaskAgentCapabilities));
        var platform = services.GetRequiredService<GitHubCopilotPlatform>();
        var taskAgentFactoryCapability = Assert.IsAssignableFrom<IHasTaskAgentFactory>(platform);

        Assert.Equal("github-copilot", platform.PlatformKey);
        AssertDirectProtocol<GitHubCopilotProtocol>(platform);
        Assert.IsNotAssignableFrom<IHasChatClientFactory>(platform);
        Assert.IsNotAssignableFrom<IHasVendorMetadata>(platform);
        Assert.IsNotAssignableFrom<IHasModelCatalog>(platform);
        Assert.IsNotAssignableFrom<IHasConfiguration>(platform);
        Assert.Equal(typeof(GitHubCopilotTaskAgentFactory), taskAgentFactoryCapability.GetTaskAgentFactory().FactoryType);
    }

    [Fact]
    public async Task AnthropicConfigurationService_GetConfigurationAsync_ShouldExposeProviderConfigurationMetadata()
    {
        using var services = CreateServices(nameof(AnthropicConfigurationService_GetConfigurationAsync_ShouldExposeProviderConfigurationMetadata));
        var configurationService = services.GetRequiredService<AnthropicConfigurationService>();

        var configuration = await configurationService.GetConfigurationAsync();

        Assert.Null(configuration.Configuration.ApiKey);
        Assert.Null(configuration.Configuration.BaseUrl);
        Assert.Collection(configuration.Properties,
            property =>
            {
                Assert.Equal(nameof(AnthropicPlatformConfiguration.ApiKey), property.Name);
                Assert.Equal(ConfigurationPropertyType.String, property.Type);
                Assert.Null(property.DefaultValue);
                Assert.True(property.Required);
            },
            property =>
            {
                Assert.Equal(nameof(AnthropicPlatformConfiguration.BaseUrl), property.Name);
                Assert.Equal(ConfigurationPropertyType.String, property.Type);
                Assert.Equal(global::Anthropic.Core.EnvironmentUrl.Production, property.DefaultValue);
                Assert.False(property.Required);
            });
    }

    [Fact]
    public async Task AnthropicModelCatalogService_GetModelCatalogAsync_ShouldRequireApiKey_BeforeLoadingModels()
    {
        var httpClientFactory = new StubHttpClientFactory(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)));
        using var services = CreateServices(
            nameof(AnthropicModelCatalogService_GetModelCatalogAsync_ShouldRequireApiKey_BeforeLoadingModels),
            serviceCollection => serviceCollection.AddSingleton<IHttpClientFactory>(httpClientFactory));
        var modelCatalogService = services.GetRequiredService<AnthropicModelCatalogService>();

        var catalog = await modelCatalogService.GetModelCatalogAsync();

        Assert.Equal(VendorModelCatalogStatus.RequiresProviderConfiguration, catalog.Status);
        Assert.Empty(catalog.Models);
        Assert.Contains(nameof(AnthropicPlatformConfiguration.ApiKey), catalog.MissingConfigurationFields!);
        Assert.Equal(0, httpClientFactory.CallCount);
    }

    [Fact]
    public async Task AnthropicModelCatalogService_GetModelCatalogAsync_ShouldReturnRemoteModels_WhenConfigurationIsSaved()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal("https://api.anthropic.com/v1/models", request.RequestUri?.ToString());
            Assert.True(request.Headers.Contains("x-api-key"));
            Assert.True(request.Headers.Contains("anthropic-version"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "data": [
                        {
                          "id": "claude-sonnet-4-20250514",
                          "display_name": "Claude Sonnet 4",
                          "created_at": "2025-01-01T00:00:00Z",
                          "type": "model"
                        },
                        {
                          "id": "claude-haiku-4.5",
                          "display_name": "Claude Haiku 4.5",
                          "created_at": "2025-01-02T00:00:00Z",
                          "type": "model"
                        }
                      ],
                      "first_id": "claude-sonnet-4-20250514",
                      "last_id": "claude-haiku-4.5",
                      "has_more": false
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var httpClientFactory = new StubHttpClientFactory(handler);
        using var services = CreateServices(
            nameof(AnthropicModelCatalogService_GetModelCatalogAsync_ShouldReturnRemoteModels_WhenConfigurationIsSaved),
            serviceCollection => serviceCollection.AddSingleton<IHttpClientFactory>(httpClientFactory));
        var configurationService = services.GetRequiredService<AnthropicConfigurationService>();
        var modelCatalogService = services.GetRequiredService<AnthropicModelCatalogService>();

        await configurationService.SetConfigurationAsync(new AnthropicPlatformConfiguration
        {
            ApiKey = "anthropic-test-key",
            BaseUrl = "https://api.anthropic.com"
        });

        var catalog = await modelCatalogService.GetModelCatalogAsync();

        Assert.Equal(VendorModelCatalogStatus.Ready, catalog.Status);
        Assert.Collection(catalog.Models,
            model =>
            {
                Assert.Equal("claude-sonnet-4-20250514", model.Id);
                Assert.Equal("Claude Sonnet 4", model.Name);
            },
            model =>
            {
                Assert.Equal("claude-haiku-4.5", model.Id);
                Assert.Equal("Claude Haiku 4.5", model.Name);
            });
        Assert.Equal(1, httpClientFactory.CallCount);
    }

    [Fact]
    public async Task AnthropicModelCatalogService_GetModelCatalogAsync_ShouldSurfaceLoadFailed_WhenRemoteRequestFails()
    {
        var httpClientFactory = new StubHttpClientFactory(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                ReasonPhrase = "Unauthorized"
            }));
        using var services = CreateServices(
            nameof(AnthropicModelCatalogService_GetModelCatalogAsync_ShouldSurfaceLoadFailed_WhenRemoteRequestFails),
            serviceCollection => serviceCollection.AddSingleton<IHttpClientFactory>(httpClientFactory));
        var configurationService = services.GetRequiredService<AnthropicConfigurationService>();
        var modelCatalogService = services.GetRequiredService<AnthropicModelCatalogService>();

        await configurationService.SetConfigurationAsync(new AnthropicPlatformConfiguration
        {
            ApiKey = "anthropic-test-key",
            BaseUrl = "https://api.anthropic.com"
        });

        var catalog = await modelCatalogService.GetModelCatalogAsync();

        Assert.Equal(VendorModelCatalogStatus.LoadFailed, catalog.Status);
        Assert.Empty(catalog.Models);
        Assert.Contains("401", catalog.Message);
    }

    [Fact]
    public async Task AnthropicPlatform_AgentFactory_ShouldRequireProviderConfiguration_InsteadOfResolvedProvider()
    {
        using var services = CreateServices(nameof(AnthropicPlatform_AgentFactory_ShouldRequireProviderConfiguration_InsteadOfResolvedProvider));
        var factory = CreateAgentFactory(services, services.GetRequiredService<AnthropicPlatform>());
        var role = new AgentRole
        {
            Name = "Claude",
            ProviderType = "anthropic",
            Config = """{"model":"claude-sonnet-4-20250514"}"""
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            factory.CreateAgentAsync(
                role,
                new AgentContext { Role = role }));

        Assert.Contains("bound provider account", exception.Message);
    }

    [Fact]
    public async Task GitHubCopilotConfigurationService_GetConfigurationAsync_ShouldReturnOptionalDefaultsMetadata_WhenFileMissing()
    {
        using var services = CreateServices(nameof(GitHubCopilotConfigurationService_GetConfigurationAsync_ShouldReturnOptionalDefaultsMetadata_WhenFileMissing));
        var configurationService = services.GetRequiredService<GitHubCopilotConfigurationService>();

        var configuration = await configurationService.GetConfigurationAsync();

        Assert.Null(configuration.Configuration.Streaming);
        Assert.Null(configuration.Configuration.AutoApproved);
        Assert.Collection(configuration.Properties,
            property =>
            {
                Assert.Equal(nameof(GitHubCopilotPlatformConfiguration.Streaming), property.Name);
                Assert.Equal(ConfigurationPropertyType.Boolean, property.Type);
                Assert.Equal(true, property.DefaultValue);
                Assert.False(property.Required);
            },
            property =>
            {
                Assert.Equal(nameof(GitHubCopilotPlatformConfiguration.AutoApproved), property.Name);
                Assert.Equal(ConfigurationPropertyType.Boolean, property.Type);
                Assert.Equal(false, property.DefaultValue);
                Assert.False(property.Required);
            });
    }

    [Fact]
    public async Task GitHubCopilotConfigurationService_SetConfigurationAsync_ShouldRoundTripConfigurationFile()
    {
        using var services = CreateServices(nameof(GitHubCopilotConfigurationService_SetConfigurationAsync_ShouldRoundTripConfigurationFile));
        var configurationService = services.GetRequiredService<GitHubCopilotConfigurationService>();

        await configurationService.SetConfigurationAsync(new GitHubCopilotPlatformConfiguration
        {
            Streaming = false,
            AutoApproved = true
        });

        var configuration = await configurationService.GetConfigurationAsync();

        Assert.False(configuration.Configuration.Streaming);
        Assert.True(configuration.Configuration.AutoApproved);
    }

    [Fact]
    public async Task GitHubCopilotConfigurationService_ConfigurableBridge_ShouldRoundTripDictionaryValues()
    {
        using var services = CreateServices(nameof(GitHubCopilotConfigurationService_ConfigurableBridge_ShouldRoundTripDictionaryValues));
        var configurationService = services.GetRequiredService<GitHubCopilotConfigurationService>();
        var configurableService = Assert.IsAssignableFrom<IVendorConfigurationService>(configurationService);

        await configurableService.SetConfigurationValuesAsync(new Dictionary<string, object?>
        {
            [nameof(GitHubCopilotPlatformConfiguration.Streaming)] = false,
            [nameof(GitHubCopilotPlatformConfiguration.AutoApproved)] = true,
        });

        var values = await configurableService.GetConfigurationValuesAsync();

        Assert.Equal(false, values[nameof(GitHubCopilotPlatformConfiguration.Streaming)]);
        Assert.Equal(true, values[nameof(GitHubCopilotPlatformConfiguration.AutoApproved)]);
    }

    [Fact]
    public async Task GitHubCopilotPlatform_AgentFactory_ShouldInjectBoundSkillDirectories()
    {
        await using var client = new CopilotClient(new CopilotClientOptions { AutoStart = false });
        using var services = CreateServices(
            nameof(GitHubCopilotPlatform_AgentFactory_ShouldInjectBoundSkillDirectories),
            serviceCollection => serviceCollection.AddSingleton<IGitHubCopilotClientHost>(new ReturningGitHubCopilotClientHost(client)));
        var factory = CreateAgentFactory(services, services.GetRequiredService<GitHubCopilotPlatform>());
        var skillDirectory = Path.Combine(_rootWorkingDirectory, "copilot-skill");
        Directory.CreateDirectory(skillDirectory);

        var role = new AgentRole
        {
            Id = Guid.NewGuid(),
            Name = "Copilot",
            ProviderType = "github-copilot",
            ModelName = "gpt-4o",
            Description = "system prompt"
        };
        var context = new AgentContext
        {
            SessionId = Guid.NewGuid(),
            AgentInstanceId = Guid.NewGuid(),
            Role = role,
            Scene = SceneType.Test
        };
        context.SetSkillRuntimePayload(new AgentSkillRuntimePayload(
        [
            new AgentSkillRuntimeEntry("maps-install", "maps", "Maps", SkillSourceKeys.SkillsSh, skillDirectory),
            new AgentSkillRuntimeEntry("maps-install-duplicate", "maps", "Maps", SkillSourceKeys.SkillsSh, skillDirectory)
        ],
        []));

        var agent = await factory.CreateAgentAsync(role, context);

        var traceAgent = Assert.IsType<GitHubCopilotTraceAgent>(agent.GetService<GitHubCopilotTraceAgent>());
        var executionConfig = traceAgent.CreateExecutionSessionConfig(null, null);
        Assert.NotNull(executionConfig.SkillDirectories);
        var actualDirectory = Assert.Single(executionConfig.SkillDirectories!);
        var expectedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetDirectoryName(skillDirectory)!));
        Assert.Equal(expectedDirectory, Path.TrimEndingDirectorySeparator(Path.GetFullPath(actualDirectory)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootWorkingDirectory))
            Directory.Delete(_rootWorkingDirectory, recursive: true);
    }

    private AgentFactory CreateAgentFactory(ServiceProvider services, params IPlatform[] platforms)
        => new(new PlatformRegistry(platforms), platforms.OfType<IAgentProvider>(), services);

    private static void AssertDirectProtocol<TProtocol>(IPlatform platform)
        where TProtocol : IProtocol
    {
        var protocol = Assert.IsAssignableFrom<IHasProtocol>(platform).GetProtocol();
        Assert.IsAssignableFrom<IProtocol>(protocol);
        Assert.IsType<TProtocol>(protocol);
    }

    private ServiceProvider CreateServices(string scopeName, Action<IServiceCollection>? configureServices = null)
    {
        var workingDirectory = Path.Combine(_rootWorkingDirectory, scopeName);
        Directory.CreateDirectory(workingDirectory);

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHttpClientFactory>(new StubHttpClientFactory(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))))
            .AddSingleton<IModelDataSource>(new StubModelDataSource())
            .AddSingleton<AnthropicPlatformMetadataService>()
            .AddSingleton<AnthropicModelCatalogService>()
            .AddSingleton<AnthropicConfigurationService>()
            .AddSingleton<AnthropicTaskAgentFactory>()
            .AddSingleton<AnthropicPlatform>()
            .AddSingleton<GitHubCopilotPlatformMetadataService>()
            .AddSingleton<GitHubCopilotModelCatalogService>()
            .AddSingleton<GitHubCopilotConfigurationService>()
            .AddSingleton<CopilotTokenService>()
            .AddSingleton<GitHubCopilotTaskAgentFactory>()
            .AddSingleton<GitHubCopilotPlatform>()
            .AddSingleton<IGitHubCopilotClientHost, StubGitHubCopilotClientHost>()
            .AddSingleton<IPermissionRequestHandler, StubPermissionRequestHandler>()
            .Configure<OpenStaffOptions>(options => options.WorkingDirectory = workingDirectory);
        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public int CallCount { get; private set; }

        public HttpClient CreateClient(string name)
        {
            CallCount++;
            return new HttpClient(_handler, disposeHandler: false);
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }

    private sealed class StubGitHubCopilotClientHost : IGitHubCopilotClientHost
    {
        public Task<CopilotClient> GetClientAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Copilot client access is not expected in these platform configuration tests.");

        public Task<bool> SessionExistsAsync(string sessionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Session access is not expected in these platform configuration tests.");

        public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Session cleanup is not expected in these platform configuration tests.");
    }

    private sealed class StubPermissionRequestHandler : IPermissionRequestHandler
    {
        public Task<PermissionAuthorizationResult> HandleAsync(PermissionAuthorizationRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Permission handling is not expected in these platform configuration tests.");

        public IDisposable Register(Func<PermissionAuthorizationRequest, CancellationToken, Task<PermissionAuthorizationResult?>> handler)
            => throw new NotSupportedException("Listener registration is not expected in these platform configuration tests.");

        public PermissionListenerLease RegisterClientListener(string? listenerId = null)
            => throw new NotSupportedException("Client listener registration is not expected in these platform configuration tests.");

        public Task<PermissionAuthorizationSubmitResult> SubmitAsync(PermissionAuthorizationResponse response, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Permission submission is not expected in these platform configuration tests.");

        public void UnregisterClientListener(string listenerId)
            => throw new NotSupportedException("Client listener registration is not expected in these platform configuration tests.");
    }

    private sealed class StubModelDataSource : IModelDataSource
    {
        public string SourceId => "stub";
        public string DisplayName => "Stub model data source";
        public bool IsReady => false;
        public DateTime? LastUpdatedUtc => null;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ModelVendor>> GetVendorsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModelVendor>>([]);

        public Task<IReadOnlyList<ModelData>> GetModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModelData>>([]);

        public Task<IReadOnlyList<ModelData>> GetModelsByVendorAsync(string vendorId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModelData>>([]);

        public Task<ModelData?> GetModelAsync(string vendorId, string modelId, CancellationToken cancellationToken = default)
            => Task.FromResult<ModelData?>(null);
    }

    private sealed class ReturningGitHubCopilotClientHost(CopilotClient client) : IGitHubCopilotClientHost
    {
        public Task<CopilotClient> GetClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(client);

        public Task<bool> SessionExistsAsync(string sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class CollapsedVendorPlatform : IPlatform, IHasVendorMetadata, IHasModelCatalog
    {
        private readonly CollapsedVendorService _service = new();

        public string PlatformKey => "collapsed";

        public IVendorPlatformMetadata GetVendorMetadataService() => _service;

        public IVendorModelCatalogService GetModelCatalogService() => _service;
    }

    private sealed class CollapsedVendorService : IVendorPlatformMetadata, IVendorModelCatalogService
    {
        public string ProviderType => "collapsed";
        public string DisplayName => "Collapsed Vendor";
        public string? AvatarDataUri => null;

        public Task<VendorModelCatalogResult> GetModelCatalogAsync(CancellationToken ct = default)
            => Task.FromResult(VendorModelCatalogResult.Ready([]));
    }
}
